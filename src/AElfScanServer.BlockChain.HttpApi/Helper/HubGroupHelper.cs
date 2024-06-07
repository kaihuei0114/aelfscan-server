namespace AElfScanServer.Common.BlockChain.HttpApi.Helper;

public class HubGroupHelper
{
    // public static string GetTransactionsGroupName(string chainId)
    // {
    //     return $"{chainId}_transactions";
    // }
    //
    
    public static string GetLatestTransactionsGroupName(string chainId)
    {
        return $"{chainId}_latest_transactions";
    }
    
    public static string GetTransactionCountPerMinuteGroupName(string chainId)
    {
        return $"{chainId}_transactions_count_per_minute";
    }

    
    public static string GetLatestBlocksGroupName(string chainId)
    {
        return $"{chainId}_latest_blocks";
    }
    
    
    public static string GetBlockOverviewGroupName(string chainId)
    {
        return $"{chainId}_block_overview";
    }
}