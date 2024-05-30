using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.BlockChain;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.Contract.Provider;
using AElfScanServer.Core;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using TokenPriceDto = AElfScanServer.Dtos.TokenPriceDto;

namespace AElfScanServer.TokenDataFunction.Service;

public interface ITokenService
{
    public Task<ListResponseDto<TokenCommonDto>> GetTokenListAsync(TokenListInput input);
    public Task<TokenDetailDto> GetTokenDetailAsync(string symbol, string chainId);
    public Task<TokenTransferInfosDto> GetTokenTransferInfosAsync(TokenTransferInput input);
    public Task<ListResponseDto<TokenHolderInfoDto>> GetTokenHolderInfosAsync(TokenHolderInput input);
    Task<TokenPriceDto> GetTokenPriceInfoAsync(CurrencyDto input);
    Task<IndexerTokenInfoDto> GetTokenBaseInfoAsync(string symbol, string chainId);
}

[Ump]
public class TokenService : ITokenService, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly ITokenHolderPercentProvider _tokenHolderPercentProvider;
    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly IContractProvider _contractProvider;


    public TokenService(ITokenIndexerProvider tokenIndexerProvider,
        ITokenHolderPercentProvider tokenHolderPercentProvider, IObjectMapper objectMapper,
        IOptionsMonitor<ChainOptions> chainOptions, ITokenPriceService tokenPriceService,
        IOptionsMonitor<TokenInfoOptions> tokenInfoOptions, ITokenInfoProvider tokenInfoProvider,
        IContractProvider contractProvider)
    {
        _objectMapper = objectMapper;
        _chainOptions = chainOptions;
        _tokenPriceService = tokenPriceService;
        _tokenInfoOptions = tokenInfoOptions;
        _tokenInfoProvider = tokenInfoProvider;
        _contractProvider = contractProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _tokenHolderPercentProvider = tokenHolderPercentProvider;
    }

    public async Task<ListResponseDto<TokenCommonDto>> GetTokenListAsync(TokenListInput input)
    {
        input.SetDefaultSort();

        var indexerTokenListDto = await _tokenIndexerProvider.GetTokenListAsync(input);

        if (indexerTokenListDto.Items.IsNullOrEmpty())
        {
            return new ListResponseDto<TokenCommonDto>();
        }

        var list = await ConvertIndexerTokenDtoAsync(indexerTokenListDto.Items, input.ChainId);

        return new ListResponseDto<TokenCommonDto>
        {
            Total = indexerTokenListDto.TotalCount,
            List = list
        };
    }

    public async Task<TokenDetailDto> GetTokenDetailAsync(string symbol, string chainId)
    {
        var indexerTokenList = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, symbol);

        AssertHelper.NotEmpty(indexerTokenList, "this token not exist");

        var list = await ConvertIndexerTokenDtoAsync(indexerTokenList, chainId);

        var tokenInfo = list[0];

        var tokenDetailDto = _objectMapper.Map<TokenCommonDto, TokenDetailDto>(tokenInfo);
        tokenDetailDto.TokenContractAddress = _chainOptions.CurrentValue.GetChainInfo(chainId)?.TokenContractAddress;
        if (_tokenInfoOptions.CurrentValue.NonResourceSymbols.Contains(symbol))
        {
            //set others
            var priceDto = await _tokenPriceService.GetTokenPriceAsync(symbol, CurrencyConstant.UsdCurrency);
            var timestamp = TimeHelper.GetTimeStampFromDateTime(DateTime.Today);
            var priceHisDto =
                await _tokenPriceService.GetTokenHistoryPriceAsync(symbol, CurrencyConstant.UsdCurrency, timestamp);
            tokenDetailDto.Price = Math.Round(priceDto.Price, CommonConstant.UsdValueDecimals);
            if (priceHisDto.Price > 0)
            {
                tokenDetailDto.PricePercentChange24h = (double)Math.Round(
                    (priceDto.Price - priceHisDto.Price) / priceHisDto.Price * 100,
                    CommonConstant.PercentageValueDecimals);
            }
        }

        return tokenDetailDto;
    }

    public async Task<TokenTransferInfosDto> GetTokenTransferInfosAsync(TokenTransferInput input)
    {
        var result = await _tokenIndexerProvider.GetTokenTransfersAsync(input);
        if (input.IsSearchAddress())
        {
            result.IsAddress = true;
            var holderInfo = await _tokenIndexerProvider.GetHolderInfoAsync(input.ChainId, input.Symbol, input.Search);
            result.Balance = holderInfo.Balance;
            var priceDto = await _tokenPriceService.GetTokenPriceAsync(input.Symbol, CurrencyConstant.UsdCurrency);
            result.Value = Math.Round(result.Balance * priceDto.Price, CommonConstant.UsdValueDecimals);
        }

        return result;
    }

    public async Task<ListResponseDto<TokenHolderInfoDto>> GetTokenHolderInfosAsync(TokenHolderInput input)
    {
        input.SetDefaultSort();

        var indexerTokenHolderInfo = await _tokenIndexerProvider.GetTokenHolderInfoAsync(input);

        var list = await ConvertIndexerTokenHolderInfoDtoAsync(indexerTokenHolderInfo.Items, input.ChainId,
            input.Symbol);

        return new ListResponseDto<TokenHolderInfoDto>
        {
            Total = indexerTokenHolderInfo.TotalCount,
            List = list
        };
    }

    public async Task<TokenPriceDto> GetTokenPriceInfoAsync(CurrencyDto input)
    {
        return await _tokenPriceService.GetTokenPriceAsync(input.BaseCurrency, input.QuoteCurrency);
    }

    public async Task<IndexerTokenInfoDto> GetTokenBaseInfoAsync(string symbol, string chainId)
    {
        var indexerTokenList = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, symbol);

        AssertHelper.NotEmpty(indexerTokenList, "this token not exist");

        return indexerTokenList[0];
    }

    private async Task<List<TokenHolderInfoDto>> ConvertIndexerTokenHolderInfoDtoAsync(
        List<IndexerTokenHolderInfoDto> indexerTokenHolderInfo, string chainId, string symbol)
    {
        var indexerTokenList = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, symbol);

        var list = new List<TokenHolderInfoDto>();

        if (indexerTokenList.IsNullOrEmpty())
        {
            return list;
        }

        var tokenSupply = indexerTokenList[0].Supply;
        var addressList = indexerTokenHolderInfo
            .Where(value => !string.IsNullOrEmpty(value.Address))
            .Select(value => value.Address).Distinct().ToList();

        var priceDtoTask = _tokenPriceService.GetTokenPriceAsync(symbol, CurrencyConstant.UsdCurrency);
        var contractInfoDictTask = _contractProvider.GetContractListAsync(chainId, addressList);

        await Task.WhenAll(priceDtoTask, contractInfoDictTask);

        var priceDto = await priceDtoTask;
        var contractInfoDict = await contractInfoDictTask;
        foreach (var indexerTokenHolderInfoDto in indexerTokenHolderInfo)
        {
            var tokenHolderInfoDto =
                _objectMapper.Map<IndexerTokenHolderInfoDto, TokenHolderInfoDto>(indexerTokenHolderInfoDto);
            tokenHolderInfoDto.Address =
                BaseConverter.OfCommonAddress(indexerTokenHolderInfoDto.Address, contractInfoDict);
            if (tokenSupply != 0)
            {
                tokenHolderInfoDto.Percentage =
                    Math.Round((decimal)indexerTokenHolderInfoDto.Amount / tokenSupply * 100,
                        CommonConstant.PercentageValueDecimals);
            }

            tokenHolderInfoDto.Value =
                Math.Round(tokenHolderInfoDto.Quantity * priceDto.Price, CommonConstant.UsdValueDecimals);
            list.Add(tokenHolderInfoDto);
        }

        return list;
    }

    private async Task<List<TokenCommonDto>> ConvertIndexerTokenDtoAsync(List<IndexerTokenInfoDto> indexerTokenList,
        string chainId)
    {
        var tokenHolderCountDic =
            await _tokenHolderPercentProvider.GetTokenHolderCount(chainId, DateTime.Now.ToString("yyyyMMdd"));

        var list = new List<TokenCommonDto>();
        foreach (var indexerTokenInfoDto in indexerTokenList)
        {
            var tokenListDto = _objectMapper.Map<IndexerTokenInfoDto, TokenCommonDto>(indexerTokenInfoDto);
            tokenListDto.TotalSupply = DecimalHelper.Divide(tokenListDto.TotalSupply, indexerTokenInfoDto.Decimals);
            tokenListDto.CirculatingSupply =
                DecimalHelper.Divide(tokenListDto.CirculatingSupply, indexerTokenInfoDto.Decimals);
            //handle image url
            tokenListDto.Token.ImageUrl = TokenInfoHelper.GetImageUrl(indexerTokenInfoDto.ExternalInfo,
                () => _tokenInfoProvider.BuildImageUrl(indexerTokenInfoDto.Symbol));
            if (tokenHolderCountDic.TryGetValue(indexerTokenInfoDto.Symbol, out var beforeCount) && beforeCount != 0)
            {
                tokenListDto.HolderPercentChange24H = Math.Round(
                    (double)(tokenListDto.Holders - beforeCount) / beforeCount * 100,
                    CommonConstant.PercentageValueDecimals);
            }

            list.Add(tokenListDto);
        }

        return list;
    }
}