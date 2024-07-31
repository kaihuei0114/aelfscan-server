using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AElf.Client.Service;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Provider;
using AElfScanServer.Common.Constant;
using AElfScanServer.Common.Contract.Provider;
using AElfScanServer.Common.Core;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.Dtos.Input;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.IndexerPluginProvider;
using AElfScanServer.Common.NodeProvider;
using AElfScanServer.Common.Options;
using AElfScanServer.Common.Token;
using AElfScanServer.Common.Token.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.HttpApi.Service;

public interface ISearchService
{
    public Task<SearchResponseDto> SearchAsync(SearchRequestDto request);
}

[Ump]
public class SearchService : ISearchService, ISingletonDependency
{
    private readonly ILogger<SearchService> _logger;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly INftInfoProvider _nftInfoProvider;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly IGenesisPluginProvider _genesisPluginProvider;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public SearchService(ILogger<SearchService> logger, ITokenIndexerProvider tokenIndexerProvider,
        IOptionsMonitor<GlobalOptions> globalOptions, INftInfoProvider nftInfoProvider,
        ITokenPriceService tokenPriceService, ITokenInfoProvider tokenInfoProvider,
        IGenesisPluginProvider genesisPluginProvider, IOptionsMonitor<TokenInfoOptions> tokenInfoOptions,
        AELFIndexerProvider aelfIndexerProvider,
        IBlockchainClientFactory<AElfClient> blockchainClientFactory)
    {
        _logger = logger;
        _tokenIndexerProvider = tokenIndexerProvider;
        _globalOptions = globalOptions;
        _nftInfoProvider = nftInfoProvider;
        _tokenPriceService = tokenPriceService;
        _tokenInfoProvider = tokenInfoProvider;
        _genesisPluginProvider = genesisPluginProvider;
        _tokenInfoOptions = tokenInfoOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
        _blockchainClientFactory = blockchainClientFactory;
    }

    public async Task<SearchResponseDto> SearchAsync(SearchRequestDto request)
    {
        var searchResp = new SearchResponseDto();
        try
        {
            //Step 1: check param
            if (!ValidParam(request.ChainId, request.Keyword))
            {
                return searchResp;
            }
            //Step 2: convert 

            //Step 3: execute query
            switch (request.FilterType)
            {
                case FilterTypes.Accounts:
                    await AssemblySearchAddressAsync(searchResp, request);
                    break;
                case FilterTypes.Contracts:
                    await AssemblySearchAddressAsync(searchResp, request);
                    break;
                case FilterTypes.Tokens:
                    await AssemblySearchTokenAsync(searchResp, request, new List<SymbolType> { SymbolType.Token });
                    break;
                case FilterTypes.Nfts:
                    await AssemblySearchTokenAsync(searchResp, request,
                        new List<SymbolType> { SymbolType.Nft, SymbolType.Nft_Collection });
                    break;
                case FilterTypes.AllFilter:
                    var tokenTask = AssemblySearchTokenAsync(searchResp, request, new List<SymbolType> { SymbolType.Token});
                    var nftTask = AssemblySearchTokenAsync(searchResp, request, new List<SymbolType> { SymbolType.Nft, SymbolType.Nft_Collection});
                    var addressTask = AssemblySearchAddressAsync(searchResp, request);
                    // var contractAddressTask = AssemblySearchContractAddressAsync(searchResp, request);
                    var txTask = AssemblySearchTransactionAsync(searchResp, request);
                    var blockTask = AssemblySearchBlockAsync(searchResp, request);
                    await Task.WhenAll(tokenTask,nftTask, addressTask, txTask, blockTask);
                    break;
            }

            return searchResp;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Execute search error.");
            return searchResp;
        }
    }

    private bool ValidParam(string chainId, string keyword)
    {
        return _globalOptions.CurrentValue.ChainIds.Exists(s => s == chainId)
               && !Regex.IsMatch(keyword, CommonConstant.SearchKeyPattern);
    }


    private async Task AssemblySearchAddressAsync(SearchResponseDto searchResponseDto, SearchRequestDto request)
    {
        TokenHolderInput holderInput;
        if (request.Keyword.Length <= CommonConstant.KeyWordAddressMinSize)
        {
            return;
        }
        holderInput = new TokenHolderInput { ChainId = request.ChainId, Address = request.Keyword };
        holderInput.SetDefaultSort();
        var tokenHolderInfos = await _tokenIndexerProvider.GetTokenHolderInfoAsync(holderInput);

        var list = tokenHolderInfos.Items.Select(i => i.Address).Distinct().ToList();

        foreach (var s in list)
        {
            if (await _genesisPluginProvider.IsContractAddressAsync(request.ChainId, s))
            {
                searchResponseDto.Contracts.Add(new SearchContract
                {
                    Address = s,
                    Name = BlockHelper.GetContractName(_globalOptions.CurrentValue, request.ChainId, s)
                });
            }
            else
            {
                searchResponseDto.Accounts.Add(s);
            }
        }
    }

    private async Task AssemblySearchTokenAsync(SearchResponseDto searchResponseDto, SearchRequestDto request,
        List<SymbolType> types)
    {
        var input = new TokenListInput { ChainId = request.ChainId, Types = types };
        if (request.SearchType == SearchTypes.ExactSearch)
        {
            input.ExactSearch = request.Keyword;
        }
        else
        {
            input.FuzzySearch = request.Keyword.ToLower();
        }
        input.SetDefaultSort();
        var indexerTokenInfoList = await _tokenIndexerProvider.GetTokenListAsync(input);
        if (indexerTokenInfoList.Items.IsNullOrEmpty())
        {
            return;
        }

        var priceDict = new Dictionary<string, CommonTokenPriceDto>();
        var symbols = indexerTokenInfoList.Items.Select(i => i.Symbol).Distinct().ToList();
        //batch query nft price
        var lastSaleInfoDict = new Dictionary<string, NftActivityItem>();
        if (types.Contains(SymbolType.Nft))
        {
            lastSaleInfoDict = await _nftInfoProvider.GetLatestPriceAsync(request.ChainId, symbols);
        }

        var elfOfUsdPriceTask = GetTokenOfUsdPriceAsync(priceDict, CurrencyConstant.ElfCurrency);
        foreach (var tokenInfo in indexerTokenInfoList.Items)
        {
            var searchToken = new SearchToken
            {
                Name = tokenInfo.TokenName, Symbol = tokenInfo.Symbol, Type = tokenInfo.Type,
                Image = TokenInfoHelper.GetImageUrl(tokenInfo.ExternalInfo,
                    () => _tokenInfoProvider.BuildImageUrl(tokenInfo.Symbol))
            };
            switch (tokenInfo.Type)
            {
                case SymbolType.Token:
                {
                    if (_tokenInfoOptions.CurrentValue.NonResourceSymbols.Contains(tokenInfo.Symbol))
                    {
                        var price = await GetTokenOfUsdPriceAsync(priceDict, tokenInfo.Symbol);
                        searchToken.Price = Math.Round(price, CommonConstant.UsdPriceValueDecimals);
                    }

                    searchResponseDto.Tokens.Add(searchToken);
                    break;
                }
                case SymbolType.Nft:
                {
                    var elfOfUsdPrice = await elfOfUsdPriceTask;
                    var elfPrice = lastSaleInfoDict.TryGetValue(tokenInfo.Symbol, out var priceDto)
                        ? priceDto.Price
                        : 0;
                    searchToken.Price = Math.Round(elfPrice * elfOfUsdPrice, CommonConstant.UsdPriceValueDecimals);
                    searchResponseDto.Nfts.Add(searchToken);
                    break;
                }
                case SymbolType.Nft_Collection:
                {
                    searchResponseDto.Nfts.Add(searchToken);
                    break;
                }
            }
        }
    }

    private async Task AssemblySearchBlockAsync(SearchResponseDto searchResponseDto, SearchRequestDto request)
    {
        if (!BlockHelper.IsBlockHeight(request.Keyword))
        {
            return;
        }

        var blockHeight = long.Parse(request.Keyword);
        var blockDtos = await _aelfIndexerProvider.GetLatestBlocksAsync(request.ChainId, blockHeight, blockHeight);

        if (!blockDtos.IsNullOrEmpty())
        {
            var blockDto = blockDtos[0];
            searchResponseDto.Block = new SearchBlock
            {
                BlockHash = blockDto.BlockHash,
                BlockHeight = blockDto.BlockHeight
            };
        }
    }

    private async Task AssemblySearchTransactionAsync(SearchResponseDto searchResponseDto, SearchRequestDto request)
    {
        if (!BlockHelper.IsTxHash(request.Keyword))
        {
            return;
        }
        var transactionResult = await  _blockchainClientFactory.GetClient(request.ChainId).GetTransactionResultAsync(request.Keyword);

        if (transactionResult.Status is "MINED" or "PENDING")
        {
            searchResponseDto.Transaction = new SearchTransaction
            {
                TransactionId = transactionResult.TransactionId,
                BlockHash = transactionResult.BlockHash,
                BlockHeight = transactionResult.BlockNumber
            };
        }
    }

    private async Task<decimal> GetTokenOfUsdPriceAsync(Dictionary<string, CommonTokenPriceDto> priceDict,
        string symbol)
    {
        if (priceDict.TryGetValue(symbol, out var priceDto))
        {
            return priceDto.Price;
        }

        priceDto = await _tokenPriceService.GetTokenPriceAsync(symbol,
            CurrencyConstant.UsdCurrency);
        priceDict[symbol] = priceDto;

        return priceDto.Price;
    }

    private Dictionary<string, string> MergeDict(string chainId, Dictionary<string, string>? contractNameDict,
        Dictionary<string, ContractInfoDto>? contractInfoDict)
    {
        var result = new Dictionary<string, string>();
        if (contractNameDict != null)
        {
            foreach (var kvp in contractNameDict)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        if (contractInfoDict != null)
        {
            foreach (var kvp in contractInfoDict)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    var name = _globalOptions.CurrentValue.GetContractName(chainId, kvp.Key);
                    result[kvp.Key] = name;
                }
            }
        }

        return result;
    }
}