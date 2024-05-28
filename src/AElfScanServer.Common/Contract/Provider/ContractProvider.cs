using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Constant;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Contract.Provider;

public interface IContractProvider
{
    Task<Dictionary<string, ContractInfoDto>> GetContractListAsync(string chainId, List<string> addressList);
}

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly GraphQlFactory _graphQlFactory;
    private readonly ILogger<ContractProvider> _logger;
    private const string IndexerType = AElfIndexerConstant.GenesisIndexer;

    public ContractProvider(GraphQlFactory graphQlFactory, ILogger<ContractProvider> logger)
    {
        _logger = logger;
        _graphQlFactory = graphQlFactory;
    }
    
    public async Task<Dictionary<string, ContractInfoDto>> GetContractListAsync(string chainId, List<string> addressList)
    {
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType).QueryAsync<IndexerContractListDto>(
                new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!,$addressList:[String!],$skipCount:Int!,$maxResultCount:Int!){
                            contractInfo(input: {chainId:$chainId,addressList:$addressList,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                                address
                                author
                                contractType
                                metadata {
                                  chainId
                                  block {
                                    blockHash
                                    blockTime
                                    blockHeight
                                  }
                                }
                            }
                        }",
                    Variables = new
                    {
                        chainId, addressList, 
                        skipCount = 0, maxResultCount = addressList.Count
                    }
                });
            return result.Items.ToDictionary(s => s.Address, s => s);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query Contract failed.");
            return new Dictionary<string, ContractInfoDto>();
        }
    }
}