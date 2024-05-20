using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Address.Provider;
using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Enums;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Redis;
using AElfScanServer.Token;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using HotChocolate.Execution;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;

namespace AElfScanServer.TokenDataFunction.Provider;

public interface ITokenAssetProvider
{
    public Task HandleDailyTokenValuesAsync(string chainId);

    public Task<AddressAssetDto> GetTokenValuesAsync(string chainId, string address);
}

public class TokenAssetProvider : RedisCacheExtension, ITokenAssetProvider, ISingletonDependency
{
    private const string LockKey = "HandleDailyTokenValues";

    private readonly ILogger<TokenAssetProvider> _logger;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly INftInfoProvider _nftInfoProvider;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    private readonly IAddressInfoProvider _addressInfoProvider;
    private readonly IAbpDistributedLock _distributedLock;
    
    public TokenAssetProvider(ITokenPriceService tokenPriceService, ILogger<TokenAssetProvider> logger, 
        ITokenIndexerProvider tokenIndexerProvider, INftInfoProvider nftInfoProvider, 
        IOptionsMonitor<TokenInfoOptions> tokenInfoOptions, IOptions<RedisCacheOptions> optionsAccessor,
        IAddressInfoProvider addressInfoProvider, IAbpDistributedLock distributedLock) : base(optionsAccessor)
    {
        _tokenPriceService = tokenPriceService;
        _logger = logger;
        _tokenIndexerProvider = tokenIndexerProvider;
        _nftInfoProvider = nftInfoProvider;
        _tokenInfoOptions = tokenInfoOptions;
        _addressInfoProvider = addressInfoProvider;
        _distributedLock = distributedLock;
    }

    public async Task HandleDailyTokenValuesAsync(string chainId)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(LockKey);
        
        var bizDate =  DateTime.Now.ToString("yyyyMMdd");
;
        var key = GetRedisKey(chainId, bizDate);
        
        await ConnectAsync();

        var isExist = await RedisDatabase.KeyExistsAsync(key);

        if (isExist)
        {
            _logger.LogWarning("Repeated execute HandleDailyTokenValues");
            return;
        }

        await HandleTokenValuesAsync(AddressAssetType.Daily, chainId);
        
        await RedisDatabase.StringSetAsync(key, 1);
    }
    
    
    public async Task<AddressAssetDto> GetTokenValuesAsync(string chainId, string address)
    {
        try
        {
            var addressAsset =  await _addressInfoProvider.GetAddressAssetAsync(AddressAssetType.Current, chainId, address);

            if (addressAsset != null)
            {
                return addressAsset;
            }

            await using var handle = await _distributedLock.TryAcquireAsync($"GetTokenValues-{address}");
        
            return await HandleTokenValuesAsync(AddressAssetType.Current, chainId, address);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenValues error, chainId {chainId}, address: {address}", chainId, address);
        }

        return new AddressAssetDto();
    }

    /**
     * if address is null, return the last address asset info
     */
    private async Task<AddressAssetDto> HandleTokenValuesAsync(AddressAssetType type, string chainId, string address = null)
    {
        var skipCount = 0;
        const int maxResultCount = CommonConstant.DefaultMaxResultCount;
        var lastAddressProcessed = new AddressAssetDto();
        //The price symbol is ELF 
        var symbolPriceDict = new Dictionary<string, decimal>();
        while (true)
        {
            //The address must be used for sorting. When the last address is the last one, submit it.
            var input = new TokenHolderInput
            {
                ChainId = chainId,
                SkipCount = skipCount,
                Address = address,
                MaxResultCount = maxResultCount,
                Types = new List<SymbolType> { SymbolType.Token, SymbolType.Nft }, 
                OrderBy = "Address",
                Sort = "Desc",
            };
            var indexerTokenHolderInfo = await _tokenIndexerProvider.GetTokenHolderInfoAsync(input);

            if (indexerTokenHolderInfo.Items.Count == 0)
            {
                break;
            }
            //address is null, need to update redis, for daily calc total value
            var valuesDict = await CalculateTokenValuesAsync(chainId, indexerTokenHolderInfo.Items, symbolPriceDict);
            
            foreach (var (valueAddress, assetDto) in valuesDict)
            {
                if (lastAddressProcessed.Address.IsNullOrEmpty())
                {
                    lastAddressProcessed = assetDto;
                    continue;
                } 

                if (lastAddressProcessed.Address == valueAddress)
                {
                    //Need to accumulate
                    lastAddressProcessed.Accumulate(assetDto);
                }
                else
                {
                    //To submit lastAddressProcessed
                    await _addressInfoProvider.CreateAddressAssetAsync(type, chainId, lastAddressProcessed);
                    lastAddressProcessed = assetDto;
                }
                
            }
            skipCount += maxResultCount;
        }
        //the end to submit lastAddressProcessed
        await _addressInfoProvider.CreateAddressAssetAsync(type, chainId, lastAddressProcessed);

        if(address.IsNullOrEmpty())
        {
            return null;
        }

        _logger.LogInformation(
            "LastAddressProcessed addressAssetDto: {totalNftValueOfElf}", JsonConvert.SerializeObject(lastAddressProcessed));
        return lastAddressProcessed;
    }

    /**
     * return OrderedDictionary, Guaranteed order of returned addresses
     */
    private async Task<OrderedDictionary<string, AddressAssetDto>> CalculateTokenValuesAsync(string chainId, List<IndexerTokenHolderInfoDto> tokenHolderInfos, 
        Dictionary<string, decimal> symbolPriceDict)
    {
        // Step 1: Filter out items with FormatAmount > 0
        var validTokenHolderInfos = tokenHolderInfos.Where(t => t.FormatAmount > 0).ToList();
        
        // Step 2: Pre process symbol price
        await PreProcessSymbolPriceAsync(chainId, symbolPriceDict, validTokenHolderInfos);
        
        _logger.LogInformation("CalculateTokenValues symbolPriceDict:{symbolPriceDict}", JsonConvert.SerializeObject(symbolPriceDict));
        
        // Step 3: Group by user address
        var userGroups = validTokenHolderInfos.GroupBy(t => t.Address);
        
        var result = new OrderedDictionary<string, AddressAssetDto>();
        foreach (var userGroup in userGroups)
        {
            double totalTokenValueOfElf = 0;
            double totalNftValueOfElf = 0;

            // Group by token type within each user group
            var typeGroups = userGroup.GroupBy(t => t.Token.Type);

            foreach (var typeGroup in typeGroups)
            {
                foreach (var tokenHolderInfo in typeGroup)
                {
                    var token = tokenHolderInfo.Token;
                    double value = 0;
                    if (symbolPriceDict.TryGetValue(token.Symbol, out var price))
                    {
                        value = (double)Math.Round(tokenHolderInfo.FormatAmount * price, CommonConstant.ElfValueDecimals);
                    }
                    if (tokenHolderInfo.Token.Type == SymbolType.Token)
                    {
                        totalTokenValueOfElf += value;
                    }
                    else if (tokenHolderInfo.Token.Type == SymbolType.Nft)
                    {
                        totalNftValueOfElf += value;
                    }
                }
            }

            var addressAsset = new AddressAssetDto(userGroup.Key, totalTokenValueOfElf, totalNftValueOfElf);
            _logger.LogInformation("CalculateTokenValues addressAsset:{addressAsset} ", JsonConvert.SerializeObject(addressAsset));
            result[userGroup.Key] = addressAsset;
        }

        return result;
    }

    private async Task PreProcessSymbolPriceAsync(string chainId, Dictionary<string, decimal> symbolPriceDict, List<IndexerTokenHolderInfoDto> tokenHolderInfos)
    {
        //Need to get Price Nft Symbols
        var getPriceNftSymbols = new HashSet<string>();

        foreach (var indexerTokenHolderInfoDto in tokenHolderInfos)
        {
            var token = indexerTokenHolderInfoDto.Token;
            //only token is NonResourceSymbols
            if (token.Type == SymbolType.Token && _tokenInfoOptions.CurrentValue.NonResourceSymbols.Contains(token.Symbol))
            {
                var priceDto = await _tokenPriceService.GetTokenPriceAsync(token.Symbol, CurrencyConstant.UsdCurrency);
                var elfPriceDto = await _tokenPriceService.GetTokenPriceAsync(CurrencyConstant.ElfCurrency, CurrencyConstant.UsdCurrency);
                symbolPriceDict[token.Symbol] = Math.Round(priceDto.Price / elfPriceDto.Price, CommonConstant.ElfValueDecimals);
            }else if (token.Type == SymbolType.Nft && !symbolPriceDict.ContainsKey(token.Symbol))
            {
                getPriceNftSymbols.Add(token.Symbol);
            }
        }
        
        //for batch query nft price
        var nftPriceDict = await _nftInfoProvider.GetLatestPriceAsync(chainId, new List<string>(getPriceNftSymbols));
        
        foreach (var (symbol, nftActivityItem) in nftPriceDict)
        {
            if (!symbolPriceDict.ContainsKey(symbol) && nftActivityItem.PriceTokenInfo != null)
            {
                //PriceTokenInfo.Symbol Maybe it always was ELF
                var priceDto = await _tokenPriceService.GetTokenPriceAsync(nftActivityItem.PriceTokenInfo.Symbol, CurrencyConstant.ElfCurrency);
                
                symbolPriceDict[symbol] = Math.Round(nftActivityItem.Price * priceDto.Price, CommonConstant.ElfValueDecimals);
            }
        }
    }
    
    
    private static string GetRedisKey(string chainId, string bizDate)
    {
        return IdGeneratorHelper.GenerateId(chainId, bizDate, LockKey);
    }
}