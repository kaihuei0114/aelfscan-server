using System.Threading.Tasks;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Dtos.Indexer;
using AElfScanServer.Constant;
using AElfScanServer.GraphQL;
using GraphQL;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using IndexerTransactionCountDto = AElfScanServer.BlockChain.Dtos.Indexer.IndexerTransactionCountDto;
using IndexerTransactionDto = AElfScanServer.BlockChain.Dtos.IndexerTransactionDto;

namespace AElfScanServer.BlockChain.Provider;

public interface IBlockChainIndexerProvider
{
    public Task<IndexerTransactionListResultDto>
        GetTransactionsAsync(string chainId, int skipCount,
            int maxResultCount, long startTime, long endTime);

    public Task<long> GetTransactionCount(string chainId);
}

public class BlockChainIndexerProvider : IBlockChainIndexerProvider, ISingletonDependency
{
    private readonly IGraphQlFactory _graphQlFactory;
    private readonly IObjectMapper _objectMapper;

    public BlockChainIndexerProvider(IGraphQlFactory graphQlFactory, IObjectMapper objectMapper)
    {
        _graphQlFactory = graphQlFactory;
        _objectMapper = objectMapper;
    }


    public async Task<IndexerTransactionListResultDto> GetTransactionsAsync(string chainId, int skipCount,
        int maxResultCount, long startTime, long endTime)
    {
        var graphQlHelper = _graphQlFactory.GetGraphQlHelper(AElfIndexerConstant.BlockChainIndexer);

        var indexerResult = await graphQlHelper.QueryAsync<IndexerTransactionResultDto>(new GraphQLRequest
        {
            Query =
                @"query($chainId:String!,$skipCount:Int!,$maxResultCount:Int!,$startTime:Long!,$endTime:Long!){
                    transactionInfos(input: {chainId:$chainId,skipCount:$skipCount,maxResultCount:$maxResultCount,startTime:$startTime,endTime:$endTime})
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
                endTime = endTime
            }
        });
        return indexerResult?.TransactionInfos;
    }


    public async Task<long> GetTransactionCount(string chainId)
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
}