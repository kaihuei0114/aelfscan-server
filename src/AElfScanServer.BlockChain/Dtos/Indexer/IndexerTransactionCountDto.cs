namespace AElfScanServer.BlockChain.Dtos.Indexer;

public class IndexerTransactionCountDto
{
    public long Count { get; set; }
}

public class IndexerTransactionCountResultDto
{
    public IndexerTransactionCountDto TransactionCount { get; set; }
}