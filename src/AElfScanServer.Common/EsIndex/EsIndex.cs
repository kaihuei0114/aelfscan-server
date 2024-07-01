using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Common.Dtos.Indexer;
using Nest;

namespace AElfScanServer.Common.EsIndex;

public class EsIndex
{
    public static ElasticClient esClient;

    public static void SetElasticClient(ElasticClient client)
    {
        esClient = client;
    }

    public static async Task<List<TransactionIndex>> GetTransactionIndexList(string chainId, long startTime,
        long endTime)
    {
        var searchResponse = esClient.Search<TransactionIndex>(s => s // 替换为你的索引名称
            .Index("transactionindex") // 替换为你的索引名称
            .Size(20000) // 限制结果数量为20000
            .Query(q => q
                .Bool(b => b
                    .Filter(f => f
                        .Range(r => r
                            .Field(t => t.BlockTime)
                            .GreaterThan(startTime)
                            .LessThan(endTime)
                        )
                    )
                    .Must(m => m
                        .Match(mm => mm
                            .Field(t => t.ChainId)
                            .Query(chainId)
                        )
                    )
                )
            )
        );


        var transactionIndices = searchResponse.Documents.ToList();

        return transactionIndices;
    }


    public static async Task<List<TransactionIndex>> GetTransactionIndexList(string chainId, string dateStr)
    {
        var searchResponse = await esClient.SearchAsync<TransactionIndex>(s => s
            .Index("transactionindex")
            .Size(20000)
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                        .Term(mm => mm
                            .Field(t => t.ChainId)
                            .Value(chainId)
                        )
                    )
                    .Must(m => m
                        .Term(terms => terms
                            .Field(t => t.DateStr)
                            .Value(dateStr)
                        )
                    )
                )
            )
        );


        var transactionIndices = searchResponse.Documents.ToList();

        return transactionIndices;
    }
}