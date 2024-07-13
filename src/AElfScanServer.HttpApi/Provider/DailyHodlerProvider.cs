using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.HttpApi.Dtos.address;
using AElfScanServer.Common.Constant;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.HttpApi.Provider;

public interface IDailyHolderProvider
{
    Task<IndexerDailyHolderDto> GetDailyHolderListAsync(string chainId);
}

public class DailyHolderProvider : IDailyHolderProvider, ISingletonDependency
{
    private readonly GraphQlFactory _graphQlFactory;
    private readonly ILogger<DailyHolderProvider> _logger;
    private const string IndexerType = AElfIndexerConstant.DailyHolderIndexer;


    public DailyHolderProvider(GraphQlFactory graphQlFactory, ILogger<DailyHolderProvider> logger)
    {
        _graphQlFactory = graphQlFactory;
        _logger = logger;
    }


    public async Task<IndexerDailyHolderDto> GetDailyHolderListAsync(string chainId)
    {
        var indexerDailyHolderDto = new IndexerDailyHolderDto();
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType).QueryAsync<IndexerDailyHolderDto>(
                new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!){
                            dailyHolder(input: {chainId:$chainId}){
                               dateStr
                               count
                            }
                        }",
                    Variables = new
                    {
                        chainId = chainId
                    }
                });
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query daily hodler failed.");
            return indexerDailyHolderDto;
        }
    }
}