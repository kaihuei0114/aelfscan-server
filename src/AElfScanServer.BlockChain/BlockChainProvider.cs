using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.BlockChain.Constant;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Dtos;
using AElfScanServer.HttpClient;
using AElfScanServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.BlockChain;

public interface IBlockChainProvider
{
    public Task<Dictionary<string, CommonAddressDto>> GetAddressDictionaryAsync(AElfAddressInput input);

    public Task<TransactionsResponseDto> GetTransactionsAsync(string chainId, string address = "",
        string transactionId = "");

    public Task<LogEventResponseDto> GetLogEventListAsync(string chainId, string address, int skipCount,
        int maxResultCount);

}

public class BlockChainProvider : IBlockChainProvider, ISingletonDependency
{
    private readonly BlockChainOption _option;
    private readonly IObjectMapper _objectMapper;
    private readonly IHttpProvider _httpProvider;

    public BlockChainProvider(IHttpProvider httpProvider, IOptionsSnapshot<BlockChainOption> apiClientOption,
        IObjectMapper objectMapper)
    {
        _httpProvider = httpProvider;
        _objectMapper = objectMapper;
        _option = apiClientOption.Value;
    }

    /*public async Task<Dictionary<string, CommonAddressDto>> GetAddressDictionaryAsync(AElfAddressInput input)
        => await _httpProvider.PostInternalServerAsync<Dictionary<string, CommonAddressDto>>(
            GenerateUrl(BlockChainConstant.AddressDicApi), input);*/
    public async Task<Dictionary<string, CommonAddressDto>> GetAddressDictionaryAsync(AElfAddressInput input)
        => new ();

    public async Task<TransactionsResponseDto> GetTransactionsAsync(string chainId, string address,
        string transactionId) => await _httpProvider.PostInternalServerAsync<TransactionsResponseDto>(
        GenerateUrl(BlockChainConstant.TransactionsUri), new TransactionsRequestDto
        {
            ChainId = chainId, Address = address, TransactionId = transactionId, BlockHeight = 100, SkipCount = 0,
            MaxResultCount = 100
        });

    public async Task<LogEventResponseDto> GetLogEventListAsync(string chainId, string address, int skipCount,
        int maxResultCount) => await _httpProvider.PostInternalServerAsync<LogEventResponseDto>(
        GenerateUrl(BlockChainConstant.LogEventsUri), new GetLogEventRequestDto
        {
            ChainId = chainId,
            ContractAddress = address,
            MaxResultCount = maxResultCount,
            SkipCount = skipCount
        });

    public async Task GetLatestBlocksAsync(AElfAddressInput input)
        => await _httpProvider.GetInternalServerAsync<CommonResponseDto<LatestBlocksRequestDto>>(
            GenerateUrl(BlockChainConstant.LatestBlocksUri), input);

    public async Task GetLatestTransactionsAsync(AElfAddressInput input)
        => await _httpProvider.GetInternalServerAsync<Dictionary<string, CommonAddressDto>>(
            GenerateUrl(BlockChainConstant.LatestTransactionsUri), input);

    public async Task GetFiltersAsync(AElfAddressInput input)
        => await _httpProvider.GetInternalServerAsync<Dictionary<string, CommonAddressDto>>(
            GenerateUrl(BlockChainConstant.FiltersUri), input);

    private string GenerateUrl(string uri) => _option.Url + uri;
}