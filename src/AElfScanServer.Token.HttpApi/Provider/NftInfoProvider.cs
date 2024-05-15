using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.GraphQL;
using AElfScanServer.HttpClient;
using AElfScanServer.Options;
using AElfScanServer.Token.Constant;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using IHttpProvider = AElfScanServer.HttpClient.New.IHttpProvider;

namespace AElfScanServer.TokenDataFunction.Provider;

public interface INftInfoProvider
{
    public Task<IndexerNftListingInfoDto> GetNftListingsAsync(GetNFTListingsDto input);

    public Task<IndexerNftActivityInfo> GetNftActivityListAsync(GetActivitiesInput input);
    
    public Task<Dictionary<string, NftCollectionInfoDto>> GetNftCollectionInfoAsync(GetNftCollectionInfoInput input);
}

public class NftInfoProvider : INftInfoProvider, ISingletonDependency
{
    private readonly ILogger<NftInfoProvider> _logger;
    private readonly IGraphQlFactory _graphQlFactory;
    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsMonitor<ApiClientOption> _apiClientOptions;

    private static readonly JsonSerializerSettings JsonSerializerSettings = JsonSettingsBuilder.New()
        .IgnoreNullValue()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .Build();

    public NftInfoProvider(IGraphQlFactory graphQlFactory, ILogger<NftInfoProvider> logger, IHttpProvider httpProvider, 
        IOptionsMonitor<ApiClientOption> apiClientOptions)
    {
        _graphQlFactory = graphQlFactory;
        _logger = logger;
        _httpProvider = httpProvider;
        _apiClientOptions = apiClientOptions;
    }
    
    private IGraphQlHelper GetGraphQlHelper()
    {
        return _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.ForestIndexer);
    }

    public async Task<IndexerNftListingInfoDto> GetNftListingsAsync(GetNFTListingsDto input)
    {
        var graphQlHelper = GetGraphQlHelper();
        try
        {
            var res = await graphQlHelper.QueryAsync<IndexerNftListingInfos>(new GraphQLRequest
            {
                Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $chainId:String,
                    $symbol:String
                ){
                  nftListingInfo(
                    input:{
                      skipCount:$skipCount,
                      maxResultCount:$maxResultCount,
                      chainId:$chainId,
                      symbol:$symbol
                    }
                  ){
                    TotalCount: totalRecordCount,
                    Message: message,
                    Items: data{
                      quantity,
                      realQuantity,
                      symbol,
                      owner,
                      prices,
                      startTime,
                      publicTime,
                      expireTime,
                      chainId,
                      purchaseToken {
      	                chainId,symbol,tokenName,
                      }
                    }
                  }
                }",
                Variables = new
                {
                    chainId = input.ChainId,
                    symbol = input.Symbol,
                    skipCount = input.SkipCount,
                    maxResultCount = input.MaxResultCount,
                }
            });
            return res?.NftListingInfo ?? new IndexerNftListingInfoDto();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetNFTListingsAsync query GraphQL error");
            throw;
        }
    }

    public async Task<IndexerNftActivityInfo> GetNftActivityListAsync(GetActivitiesInput input)
    {
        var graphQlHelper = GetGraphQlHelper();

        var indexerResult = await graphQlHelper.QueryAsync<IndexerNftActivityInfos>(new GraphQLRequest
        {
            Query = @"
			    query($skipCount:Int!,$maxResultCount:Int!,$types:[Int!],$timestampMin:Long,$timestampMax:Long,$nFTInfoId:String) {
                    nftActivityList(input:{skipCount: $skipCount,maxResultCount:$maxResultCount,types:$types,timestampMin:$timestampMin,timestampMax:$timestampMax,nFTInfoId:$nFTInfoId}){
                        totalCount:totalRecordCount,
                        items:data{
                                            nftInfoId,
                                            type,
                                            from,
                                            to,
                                            amount,
                                            price,
                                            transactionHash,
                                            timestamp,
                                            priceTokenInfo{
                                              id,
                                              chainId,
                                              blockHash,
                                              blockHeight,
                                              previousBlockHash,
                                              symbol
                                            }
                         }
                    }
                }",
            Variables = new
            {
                skipCount = input.SkipCount, maxResultCount = input.MaxResultCount, types = input.Types,
                timestampMin = input.TimestampMin, timestampMax = input.TimestampMax,
                nFTInfoId = input.NftInfoId
            }
        });
        return indexerResult?.NftActivityList ?? new IndexerNftActivityInfo();
    }

    public async Task<Dictionary<string, NftCollectionInfoDto>> GetNftCollectionInfoAsync(
        GetNftCollectionInfoInput input)
    {
        try
        {
            var resp = await _httpProvider.InvokeAsync<NftCollectionInfoResp>(
                _apiClientOptions.CurrentValue.GetApiServer(ApiInfoConstant.ForestServer).Domain,
                ApiInfoConstant.NftCollectionFloorPrice,
                body: JsonConvert.SerializeObject(input, JsonSerializerSettings));
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("GetNftCollectionInfo get failed, response:{response}",
                    (resp == null ? "non result" : resp.Code));
                return new Dictionary<string, NftCollectionInfoDto>();
            }

            return resp.Data?.Items.ToDictionary(i => i.Symbol, i => i);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetNftCollectionInfo get failed.");
            return new Dictionary<string, NftCollectionInfoDto>();
        }
    }
}