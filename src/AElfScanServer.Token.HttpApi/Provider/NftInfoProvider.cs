using System;
using System.Threading.Tasks;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.TokenDataFunction.Provider;

public interface INftInfoProvider
{
    public Task<IndexerNftListingInfoDto> GetNftListingsAsync(GetNFTListingsDto input);
}

public class NftInfoProvider : INftInfoProvider, ISingletonDependency
{
    private readonly ILogger<NftInfoProvider> _logger;
    private readonly IGraphQlFactory _graphQlFactory;

    public NftInfoProvider(IGraphQlFactory graphQlFactory, ILogger<NftInfoProvider> logger)
    {
        _graphQlFactory = graphQlFactory;
        _logger = logger;
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

    private IGraphQlHelper GetGraphQlHelper()
    {
        return _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.ForestIndexer);
    }
}