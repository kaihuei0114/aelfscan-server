using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.BlockChain.Dtos.Indexer;
using AElfScanServer.Constant;
using AElfScanServer.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.BlockChain.Provider;

public interface IBlockChainIndexerProvider
{
    public Task<IndexerTransactionListResultDto>
        GetTransactionsAsync(string chainId, int skipCount,
            int maxResultCount, long startTime = 0, long endTime = 0, string address = "");

    public Task<long> GetTransactionCount(string chainId);

    public Task<List<IndexerAddressTransactionCountDto>> GetAddressTransactionCount(string chainId,
        List<string> addressList);
}

public class BlockChainIndexerProvider : IBlockChainIndexerProvider, ISingletonDependency
{
    private readonly IGraphQlFactory _graphQlFactory;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<BlockChainIndexerProvider> _logger;

    public BlockChainIndexerProvider(IGraphQlFactory graphQlFactory, IObjectMapper objectMapper,
        ILogger<BlockChainIndexerProvider> logger)
    {
        _graphQlFactory = graphQlFactory;
        _objectMapper = objectMapper;
        _logger = logger;
    }


    public async Task<List<IndexerAddressTransactionCountDto>> GetAddressTransactionCount(string chainId,
        List<string> addressList)
    {
        var graphQlHelper = _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.BlockChainIndexer);

        var indexerResult = await graphQlHelper.QueryAsync<IndexerAddressTransactionCountResultDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$addressList:[String!]){
                    addressTransactionCount(input: {chainId:$chainId,addressList:$addressList})
                {
         
                    items {
                          count
                          chainId
                          address
                         
                    }
                }
            }",
            Variables = new
            {
                chainId = chainId, addressList = addressList
            }
        });
        return indexerResult.AddressTransactionCount.Items;
    }

    public async Task<IndexerTransactionListResultDto> GetTransactionsAsync(string chainId, int skipCount,
        int maxResultCount, long startTime = 0, long endTime = 0, string address = "")
    {
        var graphQlHelper = _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.BlockChainIndexer);

        var indexerResult = await graphQlHelper.QueryAsync<IndexerTransactionResultDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$skipCount:Int!,$maxResultCount:Int!,$startTime:Long!,$endTime:Long!,$address:String!){
                    transactionInfos(input: {chainId:$chainId,skipCount:$skipCount,maxResultCount:$maxResultCount,startTime:$startTime,endTime:$endTime,address:$address})
                {
                  totalCount
                    items {
                       transactionId
                          blockHeight
                          chainId
                          methodName
                          status
                          from
                          to
                          transactionValue
                          metadata {
                            chainId
                            block {
                              blockHash
                              blockHeight
                              blockTime
                            }
                          }
                    }
                }
            }",
            Variables = new
            {
                chainId = chainId, skipCount = skipCount, maxResultCount = maxResultCount, startTime = startTime,
                endTime = endTime, address = address
            }
        });
        return indexerResult?.TransactionInfos;
    }


    public async Task<long> GetTransactionCount(string chainId)
    {
        try
        {
            var graphQlHelper = _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.BlockChainIndexer);

            var indexerResult = await graphQlHelper.QueryAsync<IndexerTransactionCountResultDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!){
                    transactionCount(input: {chainId:$chainId})
                {
                   count
                }
            }",
                Variables = new
                {
                    chainId = chainId,
                }
            });
            return indexerResult.TransactionCount.Count;
        }
        catch (Exception e)
        {
            _logger.LogError("Get transaction count error from blockchain app plugin:{e}", e.Message);
        }

        return 0;
    }
}