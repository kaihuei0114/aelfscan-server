using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AElf.Client.Service;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Constant;
using AElfScanServer.Contract.Provider;
using AElfScanServer.Dtos;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BlockChain.HttpApi.Service;

public interface ISearchService
{
    public Task<SearchResponseDto> SearchAsync(SearchRequestDto request);
}

public class SearchService : ISearchService, ISingletonDependency
{
    private readonly ILogger<SearchService> _logger;
    private readonly IOptionsMonitor<BlockChainOptions> _blockChainOptions;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly INftInfoProvider _nftInfoProvider;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly IContractProvider _contractProvider;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    
    public SearchService(ILogger<SearchService> logger, ITokenIndexerProvider tokenIndexerProvider,
        IOptionsMonitor<BlockChainOptions> blockChainOptions, INftInfoProvider nftInfoProvider, 
        ITokenPriceService tokenPriceService, ITokenInfoProvider tokenInfoProvider, 
        IContractProvider contractProvider, IOptionsMonitor<TokenInfoOptions> tokenInfoOptions, 
        AELFIndexerProvider aelfIndexerProvider)
    {
        _logger = logger;
        _tokenIndexerProvider = tokenIndexerProvider;
        _blockChainOptions = blockChainOptions;
        _nftInfoProvider = nftInfoProvider;
        _tokenPriceService = tokenPriceService;
        _tokenInfoProvider = tokenInfoProvider;
        _contractProvider = contractProvider;
        _tokenInfoOptions = tokenInfoOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
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
            //TODO
            //request.Keyword = request.Keyword.ToLower();

            //Step 3: execute query
            switch (request.FilterType)
            {
                case FilterTypes.Accounts:
                    await AssemblySearchAddressAsync(searchResp, request);
                    break;
                case FilterTypes.Contracts:
                    await AssemblySearchContractAddressAsync(searchResp, request);
                    break;
                case FilterTypes.Tokens:
                    await AssemblySearchTokenAsync(searchResp, request, new List<SymbolType> { SymbolType.Token });
                    break;
                case FilterTypes.Nfts:
                    await AssemblySearchTokenAsync(searchResp, request, new List<SymbolType> { SymbolType.Nft });
                    break;
                case FilterTypes.AllFilter:
                    var types = new List<SymbolType> { SymbolType.Token, SymbolType.Nft };
                    var tokenTask = AssemblySearchTokenAsync(searchResp, request, types);
                    var addressTask = AssemblySearchAddressAsync(searchResp, request);
                    var contractAddressTask = AssemblySearchContractAddressAsync(searchResp, request);
                    var txTask = AssemblySearchTransactionAsync(searchResp, request);
                    var blockTask = AssemblySearchBlockAsync(searchResp, request);
                    await Task.WhenAll(tokenTask, addressTask, contractAddressTask, txTask, blockTask);
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
        return _blockChainOptions.CurrentValue.ValidChainIds.Exists(s => s == chainId)
               && !Regex.IsMatch(keyword, CommonConstant.SearchKeyPattern);
    }
    
    private async Task AssemblySearchContractAddressAsync(SearchResponseDto searchResponseDto, SearchRequestDto request)
    {
        var contractInfoDict = await _contractProvider.GetContractListAsync(request.ChainId, new List<string> { request.Keyword });
        searchResponseDto.Contracts = contractInfoDict.Values.Select(i => new SearchContract
        {
            Name = _blockChainOptions.CurrentValue.GetContractName(request.ChainId, i.Address),
            Address = i.Address
        }).ToList();
    }
    
    private async Task AssemblySearchAddressAsync(SearchResponseDto searchResponseDto, SearchRequestDto request)
    {
        var holderInput = new TokenHolderInput
        {
            ChainId = request.ChainId, 
            Search = request.Keyword
        };
        holderInput.SetDefaultSort();
        var tokenHolderInfos = await _tokenIndexerProvider.GetTokenHolderInfoAsync(holderInput);
        searchResponseDto.Accounts = tokenHolderInfos.Items.Select(i => i.Address).ToList();
    }

    private async Task AssemblySearchTokenAsync(SearchResponseDto searchResponseDto, SearchRequestDto request,
        List<SymbolType> types)
    {
        var input = new TokenListInput()
        {
            ChainId = request.ChainId,
            Types = types,
            //TODO
            Search = request.Keyword
        };
        var indexerTokenInfoList = await _tokenIndexerProvider.GetTokenListAsync(input);
        if (indexerTokenInfoList.Items.IsNullOrEmpty())
        {
            return;
        }

        var priceDict = new Dictionary<string, TokenPriceDto>();
        var symbols = indexerTokenInfoList.Items.Select(i => i.Symbol).Distinct().ToList();
        //batch query nft price
        var lastSaleInfoDict = new Dictionary<string, NftActivityItem>();
        if (types.Contains(SymbolType.Nft))
        {
            lastSaleInfoDict = await _nftInfoProvider.GetLatestPriceAsync(request.ChainId, symbols);
        }

        foreach (var tokenInfo in indexerTokenInfoList.Items)
        {
            var searchToken = new SearchToken
            {
                Name = tokenInfo.TokenName,
                Symbol = tokenInfo.Symbol,
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
                    var elfOfUsdPrice = await GetTokenOfUsdPriceAsync(priceDict, CurrencyConstant.ElfCurrency);
                    var elfPrice = lastSaleInfoDict.TryGetValue(tokenInfo.Symbol, out var priceDto)
                        ? priceDto.Price : 0;
                    searchToken.Price = Math.Round(elfPrice * elfOfUsdPrice, CommonConstant.UsdPriceValueDecimals);
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
        
        var aElfClient = new AElfClient(_blockChainOptions.CurrentValue.ChainNodeHosts[request.ChainId]);

        var transactionResult = await aElfClient.GetTransactionResultAsync(request.Keyword);

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

    private async Task<decimal> GetTokenOfUsdPriceAsync(Dictionary<string, TokenPriceDto> priceDict, string symbol)
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
}