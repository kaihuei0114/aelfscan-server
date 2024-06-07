using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.Constant;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Common.Address.HttpApi.Provider;

public interface IIndexerGenesisProvider
{
    Task<ContractInfoDto> GetContractAsync(string chainId, string address, int skipCount = 0, int maxResultCount = 10);

    Task<IndexerContractListResultDto> GetContractListAsync(string chainId, int skipCount,
        int maxResultCount, string orderBy, string sort);

    Task<List<ContractRecordDto>> GetContractRecordAsync(string chainId, string address, int skipCount = 0,
        int maxResultCount = 10);

    Task<List<ContractRegistrationDto>> GetContractRegistrationAsync(string chainId, string codeHash,
        int skipCount = 0, int maxResultCount = 0);
}

public class IndexerGenesisProvider : IIndexerGenesisProvider, ISingletonDependency
{
    private readonly GraphQlFactory _graphQlFactory;
    private readonly ILogger<IndexerGenesisProvider> _logger;
    private const string IndexerType = AElfIndexerConstant.GenesisIndexer;

    public IndexerGenesisProvider(GraphQlFactory graphQlFactory, ILogger<IndexerGenesisProvider> logger)
    {
        _logger = logger;
        _graphQlFactory = graphQlFactory;
    }

    public async Task<IndexerContractListResultDto> GetContractListAsync(string chainId,
        int skipCount,
        int maxResultCount, string orderBy = "", string sort = "")
    {
        var indexerContractListResultDto = new IndexerContractListResultDto();
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType).QueryAsync<IndexerContractListResultDto>(
                new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!,$orderBy:String,$sort:String,$skipCount:Int!,$maxResultCount:Int!){
                            contractList(input: {chainId:$chainId,orderBy:$orderBy,sort:$sort,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                               totalCount
                               items {
                                 address
                                contractVersion
                                version
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
                            }
                        }",
                    Variables = new
                    {
                        chainId = chainId, orderBy = orderBy, sort = sort, skipCount = skipCount,
                        maxResultCount = maxResultCount
                    }
                });
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query ContractList failed.");
            return indexerContractListResultDto;
        }
    }

    public async Task<ContractInfoDto> GetContractAsync(string chainId, string address, int skipCount,
        int maxResultCount)
    {
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType).QueryAsync<IndexerContractListDto>(
                new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!,$address:String!,$skipCount:Int!,$maxResultCount:Int!){
                            contractInfo(input: {chainId:$chainId,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                                address
                                author
                                codeHash
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
                        chainId = chainId, address = address, skipCount = skipCount, maxResultCount = maxResultCount
                    }
                });
            return result.Items.Count > 0 ? result.Items[0] : new ContractInfoDto();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query Contract failed.");
            return new ContractInfoDto();
        }
    }

    public async Task<List<ContractRecordDto>> GetContractRecordAsync(string chainId, string address, int skipCount = 0,
        int maxResultCount = 10)
    {
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType).QueryAsync<IndexerContractRecordListDto>(
                new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!,$address:String!,$skipCount:Int!,$maxResultCount:Int!){
                            contractRecord(input: {chainId:$chainId,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                            operator
                            operationType
                            transactionId
                            contractInfo {
                              address
                              codeHash
                              author
                              version
                              nameHash
                              contractVersion
                              contractCategory
                              contractType
                            }
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
                        chainId = chainId, address = address, skipCount = skipCount, maxResultCount = maxResultCount
                    }
                });
            return result.ContractRecord;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query Contract failed.");
            return new List<ContractRecordDto>();
        }
    }

    public async Task<List<ContractRegistrationDto>> GetContractRegistrationAsync(string chainId, string codeHash,
        int skipCount, int maxResultCount)
    {
        try
        {
            var result = await _graphQlFactory.GetGraphQlHelper(IndexerType)
                .QueryAsync<IndexerContractRegistrationListDto>(new GraphQLRequest
                {
                    Query =
                        @"query($chainId:String!,$codeHash:String!){
                            contractRegistration(input: {chainId:$chainId,codeHash:$codeHash}){
                                codeHash,
                                code  
                            }
                        }",
                    Variables = new
                    {
                        chainId = chainId, codeHash = codeHash, skipCount = skipCount, maxResultCount = maxResultCount
                    }
                });
            return result.ContractRegistration;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query Contract Registration failed.");
            return new List<ContractRegistrationDto>();
        }
    }
}