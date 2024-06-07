using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AElfScanServer.Token.Constant;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.HttpClient;
using AElfScanServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Common.Token;

public interface ITokenProvider
{
    Task<TokenInfoListDto> GetTokenListByAddressAsync(GetTokenListInput input);
    Task<GetNftCollectionListResponseDto> GetNftListByAddressAsync(GetNftListInput input);
    Task<TokenTransferInfosDto> GetTransferListByAddressAsync(TokenTransferInput input);
    Task<IndexerTokenInfoDto> GetTokenInfoAsync(string chainId, string symbol);
    public Task<BinancePriceDto> GetTokenPriceInfoAsync(CurrencyDto input);
}

public class TokenProvider : ITokenProvider, ISingletonDependency
{
    private ILogger<TokenProvider> _logger;
    private readonly TokenServerOption _option;
    private readonly IHttpProvider _httpProvider;

    public TokenProvider(ILogger<TokenProvider> logger, IOptionsSnapshot<TokenServerOption> tokenServerOption,
        IHttpProvider httpProvider)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _option = tokenServerOption.Value;
    }

    public async Task<TokenInfoListDto> GetTokenListByAddressAsync(GetTokenListInput input)
        => await PostAsync<TokenInfoListDto>(TokenServerConstant.TokenListByAddress, input);

    public async Task<GetNftCollectionListResponseDto> GetNftListByAddressAsync(GetNftListInput input)
        => await PostAsync<GetNftCollectionListResponseDto>(TokenServerConstant.NftListByAddress, input);

    public async Task<TokenTransferInfosDto> GetTransferListByAddressAsync(TokenTransferInput input)
        => await _httpProvider.PostInternalServerAsync<TokenTransferInfosDto>(
            _option.Url + TokenServerConstant.TokenTransfersByAddress, input);

    //     => await _httpProvider.PostInternalServerAsync<TransactionsResponseDto>(
    //     GenerateUrl(BlockChainConstant.TransactionsUri), new TransactionsRequestDto
    // {
    //     ChainId = chainId, Address = address, TransactionId = transactionId, BlockHeight = 100, SkipCount = 0,
    //     MaxResultCount = 999
    // });
    public async Task<IndexerTokenInfoDto> GetTokenInfoAsync(string chainId, string symbol)
        => await GetAsync<IndexerTokenInfoDto>(TokenServerConstant.TokenInfo, new Dictionary<string, string>
        {
            { "chainId", chainId },
            { "symbol", symbol }
        });

    public async Task<BinancePriceDto> GetTokenPriceInfoAsync(CurrencyDto input)
    {
        return await _httpProvider.InvokeAsync<BinancePriceDto>(_option.Url,
            new ApiInfo(HttpMethod.Post, TokenServerConstant.TokenPrice), param: input);
    }

    private async Task<T> PostAsync<T>(string uri, object parameters)
    {
        var result =
            await _httpProvider.PostAsync<CommonResponseDto<T>>(_option.Url + uri, RequestMediaType.Json, parameters);
        if (result.Code != "20000")
        {
            throw new UserFriendlyException($"Token service post request failed, message:{result.Message}.");
        }

        return result.Data;
    }

    private async Task<T> GetAsync<T>(string uri, object param)
    {
        var result = await _httpProvider.InvokeAsync<CommonResponseDto<T>>(_option.Url,
            new ApiInfo(HttpMethod.Get, uri), param: param);

        if (result.Code != "20000")
        {
            throw new UserFriendlyException($"Token service get request failed, message:{result.Message}.");
        }

        return result.Data;
    }
}