using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.Enums;
using AElfScanServer.GraphQL;
using AElfScanServer.Helper;
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

    public Task<Dictionary<string, NftActivityItem>> GetLatestPriceAsync(string chainId, List<string> symbols);
    
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
                                            blockHeight,
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

    public async Task<Dictionary<string, NftActivityItem>> GetLatestPriceAsync(string chainId, List<string> symbols)
    {
        try
        {
            var graphQlHelper = GetGraphQlHelper();
            var queries = new List<string>();
            var variables = new Dictionary<string, object>
            {
                { "skipCount", 0 },
                { "maxResultCount", 1 },
                { "types", new List<int> { (int)NftActivityType.Sale } }
            };

            foreach (var symbol in symbols)
            {
                var nftInfoId = IdGeneratorHelper.GetNftInfoId(chainId, symbol);
                var fieldName = symbol.Replace("-", "_"); // replace valid char
                queries.Add($@"
            {fieldName}: nftActivityList(input: {{
                skipCount: $skipCount, 
                maxResultCount: $maxResultCount, 
                types: $types, 
                nFTInfoId: ""{nftInfoId}""
            }}) {{
                items:data {{
                    nftInfoId,
                    type,
                    from,
                    to,
                    amount,
                    price,
                    transactionHash,
                    timestamp,
                    blockHeight,
                    priceTokenInfo {{
                        id,
                        chainId,
                        blockHash,
                        blockHeight,
                        previousBlockHash,
                        symbol
                    }}
                }}
            }}");
            }

            var query = $@"
            query($skipCount:Int!, $maxResultCount:Int!, $types:[Int!]) {{
            {string.Join("\n", queries)}
        }}";

            var indexerResult = await graphQlHelper.QueryAsync<Dictionary<string, IndexerNftActivityInfo>>(new GraphQLRequest
            {
                Query = query,
                Variables = variables
            });
        
            if (indexerResult == null)
            {
                return new Dictionary<string, NftActivityItem>();
            }

            var result = symbols.ToDictionary(
                symbol => symbol,
                symbol =>
                {
                    var fieldName = symbol.Replace("-", "_");
                    return indexerResult.TryGetValue(fieldName, out var activityList) && activityList.Items.Any()
                        ? activityList.Items.First()
                        : new NftActivityItem();
                });

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetLatestPrice error");
            return new Dictionary<string, NftActivityItem>();
        }
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