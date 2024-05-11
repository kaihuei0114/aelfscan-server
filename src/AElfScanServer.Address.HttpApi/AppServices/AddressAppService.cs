using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.Token;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.Address.HttpApi.AppServices;

public interface IAddressAppService
{
    Task<GetAddressListResultDto> GetAddressListAsync(GetListInputInput input);
    Task<GetAddressDetailResultDto> GetAddressDetailAsync(GetAddressDetailInput input);
    Task<GetAddressTokenListResultDto> GetAddressTokenListAsync(GetAddressTokenListInput input);
    Task<GetTransferListResultDto> GetTransferListAsync(GetTransferListInput input);
    Task<GetTransactionListResultDto> GetTransactionListAsync(GetTransactionListInput input);
}

public class AddressAppService : IAddressAppService
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<AddressAppService> _logger;
    private readonly IBlockChainProvider _blockChainProvider;
    private readonly IIndexerGenesisProvider _indexerGenesisProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    
    public AddressAppService(IObjectMapper objectMapper, ILogger<AddressAppService> logger, 
        BlockChainProvider blockChainProvider, IIndexerGenesisProvider indexerGenesisProvider, 
        ITokenIndexerProvider tokenIndexerProvider, ITokenPriceService tokenPriceService, 
        ITokenInfoProvider tokenInfoProvider, IOptionsMonitor<TokenInfoOptions> tokenInfoOptions)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _blockChainProvider = blockChainProvider;
        _indexerGenesisProvider = indexerGenesisProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _tokenPriceService = tokenPriceService;
        _tokenInfoProvider = tokenInfoProvider;
        _tokenInfoOptions = tokenInfoOptions;
    }

    public async Task<GetAddressListResultDto> GetAddressListAsync(GetListInputInput input)
    {
        var holderInput = new TokenHolderInput
        {
            ChainId = input.ChainId, Symbol = CurrencyConstant.ElfCurrency,
            SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount
        };
        holderInput.SetDefaultSort();
        var indexerTokenHolderInfo = await _tokenIndexerProvider.GetTokenHolderInfoAsync(holderInput);
        var indexerTokenList =
            await _tokenIndexerProvider.GetTokenDetailAsync(input.ChainId, CurrencyConstant.ElfCurrency);
        var tokenInfo = indexerTokenList[0];
        var result = new GetAddressListResultDto
        {
            Total = indexerTokenHolderInfo.TotalCount,
            TotalBalance = DecimalHelper.Divide(tokenInfo.Supply, tokenInfo.Decimals)
        };
        var addressInfos = await _blockChainProvider.GetAddressDictionaryAsync(new AElfAddressInput
        {
            ChainId = input.ChainId,
            Addresses = indexerTokenHolderInfo.Items.Select(address => address.Address).ToList()
        });
        var addressList = new List<GetAddressInfoResultDto>();
        foreach (var info in indexerTokenHolderInfo.Items)
        {
            var addressResult = _objectMapper.Map<IndexerTokenHolderInfoDto, GetAddressInfoResultDto>(info);
            addressResult.Percentage = Math.Round((decimal)info.Amount / tokenInfo.Supply * 100, CommonConstant.PercentageValueDecimals);
            if (addressInfos.TryGetValue(info.Address, out var addressInfo))
            {
                addressResult.AddressType = addressInfo.AddressType;
            }
            addressList.Add(addressResult);
        }
        //add sort 
        addressList = addressList.OrderByDescending(item => item.Balance)   
            .ThenByDescending(item => item.TransactionCount)
            .ToList();
        result.List = addressList;
        return result;
    }

    public async Task<GetAddressDetailResultDto> GetAddressDetailAsync(GetAddressDetailInput input)
    {
        _logger.LogInformation("GetAddressDetailAsync");
        var result = new GetAddressDetailResultDto();

        if (input.AddressType is AddressType.ContractAddress)
        {
            var contractInfo = await _indexerGenesisProvider.GetContractAsync(input.ChainId, input.Address);
            result = _objectMapper.Map<ContractInfoDto, GetAddressDetailResultDto>(contractInfo);
            var addressInfos = await _blockChainProvider.GetAddressDictionaryAsync(new AElfAddressInput
            {
                Addresses = new List<string>(new[] { input.Address })
            });

            result.ContractName = addressInfos.TryGetValue(input.Address, out var addressInfo)
                ? addressInfo.Name
                : "ContractName";
            // todo: indexer add time sort
            /*var contractRecords = await _indexerGenesisProvider.GetContractRecordAsync(input.ChainId, input.Address);
            if (contractRecords.Count > 0)
            {
                result.ContractTransactionHash = contractRecords[0].TransactionId;
            }*/
        }
        var holderInfo = await _tokenIndexerProvider.GetHolderInfoAsync(input.ChainId, CurrencyConstant.ElfCurrency, input.Address);
        var priceDto = await _tokenPriceService.GetTokenPriceAsync(CurrencyConstant.ElfCurrency, CurrencyConstant.UsdCurrency);
        result.ElfBalance = holderInfo.Balance;
        result.ElfPriceInUsd = Math.Round(priceDto.Price, CommonConstant.UsdValueDecimals);
        result.ElfBalanceOfUsd = Math.Round(holderInfo.Balance * priceDto.Price, CommonConstant.UsdValueDecimals);
        
        var holderInfos = await _tokenIndexerProvider.GetHolderInfoAsync(input.ChainId, input.Address);
        result.TokenHoldings = holderInfos.Count;

        var transferInput = new TokenTransferInput()
        {
            ChainId = input.ChainId
        };
        transferInput.SetDefaultSort();
        var tokenTransferListDto = await _tokenIndexerProvider.GetTokenTransferInfoAsync(transferInput);

        if (!tokenTransferListDto.Items.IsNullOrEmpty())
        {
            var transferInfoDto = tokenTransferListDto.Items[0];
            result.LastTransactionSend = new TransactionInfoDto
            {
                TransactionId = transferInfoDto.TransactionId,
                BlockHeight = transferInfoDto.Metadata.Block.BlockHeight,
                BlockTime = transferInfoDto.Metadata.Block.BlockTime
            };
            //TODO
            result.FirstTransactionSend = new TransactionInfoDto
            {
                TransactionId = transferInfoDto.TransactionId,
                BlockHeight = transferInfoDto.Metadata.Block.BlockHeight,
                BlockTime = transferInfoDto.Metadata.Block.BlockTime
            };
        }
        return result;
    }

    public async Task<GetAddressTokenListResultDto> GetAddressTokenListAsync(
        GetAddressTokenListInput input)
    {
        var tokenHolderInput = _objectMapper.Map<GetAddressTokenListInput, TokenHolderInput>(input);
        tokenHolderInput.SetDefaultSort();
        var holderInfos = await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);
        var list = new List<TokenInfoDto>();
        if (holderInfos.Items.IsNullOrEmpty())
        {
            return new GetAddressTokenListResultDto();
        }
        var tokenDict = await _tokenIndexerProvider.GetTokenDictAsync(input.ChainId, 
            holderInfos.Items.Select(i => i.Token.Symbol).ToList());
        var elfPriceDto = await _tokenPriceService.GetTokenPriceAsync(CurrencyConstant.ElfCurrency, CurrencyConstant.UsdCurrency);
        foreach (var holderInfo in holderInfos.Items)
        {
            var symbol = holderInfo.Token.Symbol;
            var tokenHolderInfo = _objectMapper.Map<IndexerTokenHolderInfoDto, TokenInfoDto>(holderInfo);
            if (tokenDict.TryGetValue(symbol, out var tokenInfo))
            {
                //handle image url
                tokenHolderInfo.Token.Name = tokenInfo.TokenName;
                tokenHolderInfo.Token.ImageUrl = TokenInfoHelper.GetImageUrl(tokenInfo.ExternalInfo,
                    () => _tokenInfoProvider.BuildImageUrl(tokenInfo.Symbol));    
            }
            if (_tokenInfoOptions.CurrentValue.NonResourceSymbols.Contains(symbol))
            { 
                var priceDto = await _tokenPriceService.GetTokenPriceAsync(symbol, CurrencyConstant.UsdCurrency);
                var timestamp = TimeHelper.GetTimeStampFromDateTime(DateTime.Today);
                var priceHisDto = await _tokenPriceService.GetTokenHistoryPriceAsync(symbol, CurrencyConstant.UsdCurrency, timestamp);
                tokenHolderInfo.PriceOfUsd = Math.Round(priceDto.Price, CommonConstant.UsdValueDecimals);
                tokenHolderInfo.ValueOfUsd = Math.Round(tokenHolderInfo.Quantity * priceDto.Price, CommonConstant.UsdValueDecimals);
                tokenHolderInfo.PriceOfElf = Math.Round(priceDto.Price / elfPriceDto.Price, CommonConstant.ElfValueDecimals);
                tokenHolderInfo.ValueOfElf = Math.Round(tokenHolderInfo.Quantity * priceDto.Price / elfPriceDto.Price, CommonConstant.ElfValueDecimals);
                if (priceHisDto.Price > 0)
                {
                    tokenHolderInfo.PriceOfUsdPercentChange24h = (double)Math.Round((priceDto.Price - priceHisDto.Price) / priceHisDto.Price  * 100, 
                        CommonConstant.PercentageValueDecimals);
                }
            }
            list.Add(tokenHolderInfo);
        }
        var result = new GetAddressTokenListResultDto
        {
            AssetInUsd = list.Sum(i => i.ValueOfUsd),
            Total = holderInfos.TotalCount,
            List = list
        };
        return result;
    }

    public async Task<GetTransferListResultDto> GetTransferListAsync(GetTransferListInput input)
    {
        try
        {
            
            var result = new GetTransferListResultDto();

            /*_logger.LogInformation("GetTransferListByAddressAsync");

            var getAddressTransferListInput = _objectMapper.Map<GetTransferListInput, TokenTransferInput>(input);

            switch (input.TokenType)
            {
                case TokenType.Token:
                    getAddressTransferListInput.Types = new List<SymbolType> { SymbolType.Token };
                    var getTokenTransferListResult =
                        await _tokenProvider.GetTransferListByAddressAsync(getAddressTransferListInput);
                    result.Tokens = getTokenTransferListResult.List;
                    break;
                case TokenType.Nft:
                    getAddressTransferListInput.Types = new List<SymbolType> { SymbolType.Nft };
                    var getNftTransferListResult =
                        await _tokenProvider.GetTransferListByAddressAsync(getAddressTransferListInput);
                    result.Nfts = getNftTransferListResult.List;
                    break;
                default:
                    throw new UserFriendlyException("Unsupported token type!");
            }*/

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTransferListByAddressAsync failed");
            throw;
        }
    }

    public async Task<GetTransactionListResultDto> GetTransactionListAsync(GetTransactionListInput input)
        => _objectMapper.Map<TransactionsResponseDto, GetTransactionListResultDto>(
            await _blockChainProvider.GetTransactionsAsync(input.ChainId, input.Address));
}