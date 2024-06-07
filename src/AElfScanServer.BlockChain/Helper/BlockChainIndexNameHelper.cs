namespace AElfScanServer.Common.BlockChain.Helper;

public class BlockChainIndexNameHelper
{
    public static string GenerateAddressIndexName(string chainId)
    {
        return $"{chainId.ToLower()}_address";
    }


    public static string GenerateTokenIndexName(string chainId)
    {
        return $"{chainId.ToLower()}_token";
    }

    public static string GenerateLogEventIndexName(string chainId)
    {
        return $"{chainId.ToLower()}_logevent";
    }


    public static string GenerateTransactionIndexName(string chainId)
    {
        return $"{chainId.ToLower()}_transaction";
    }

    public static string GenerateBlockExtraIndexName(string chainId)
    {
        return $"{chainId.ToLower()}_block_extra";
    }
}

public class RedisKeyHelper
{
    public static string TransactionChartData(string chainId)
    {
        return $"explore_{chainId}_transaction_chart";
    }
    
    public static string RewardKey(string chainId)
    {
        return $"explore_{chainId}_rewardKey";
    }


    public static string BlockRewardKey(string chainId, long blockHeight)
    {
        return $"explore_{chainId}_block_reward_{blockHeight}";
    }


    public static string TokenInfoKey(string chainId, string symbol)
    {
        return $"explore_{chainId}_token_info_{symbol}";
    }


    public static string TokenUsdPriceKey(string symbol)
    {
        return $"explore_token_usdPrice_{symbol}";
    }


    public static string AelfPriceKey(string chainId)
    {
        return $"explore_{chainId}_aelf_price";
    }


    public static string AelfPrice24RateKey(string chainId)
    {
        return $"explore_{chainId}_aelf_price_24_rate";
    }


    public static string BlockHeightKey(string chainId)
    {
        return $"explore_{chainId}_blockheight";
    }


    public static string AddressKey(string address, string chainId)
    {
        return $"explore_{chainId}_address_{address}";
    }


    public static string TransactionTPS(string chainId)
    {
        return $"explore_{chainId}_transaction_tps";
    }


    public static string PullBlockHeight(string chainId)
    {
        return $"explore_{chainId}_pull_blockHeight";
    }
}
