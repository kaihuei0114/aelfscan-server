using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Options;

public class BlockChainOptions
{
    public const string PrivateKey = "09da44778f8db2e602fb484334f37df19e221c84c4582ce5b7770ccfbc3ddbef";
    public int MaxResultCount { get; set; }
    public List<string> ValidChainIds { get; set; }

    public Dictionary<string, string> TokenImageUrls { get; set; }
    public Dictionary<string, string> ChainNodeHosts { get; set; }

    public long TransactionCountCacheExpiration { get; set; }

    public long AddressCountCacheExpiration { get; set; }

    public long TokenUsdPriceExpireDurationSeconds { get; set; }

    public string ContractAddressTreasury { get; set; }
    public string ContractAddressConsensus { get; set; }

    public string BNApiKey { get; set; }
    public string BNSecretKey { get; set; }
    public string BNBaseUrl { get; set; }

    public long RewardCacheExpiration { get; set; }
    public long TransactionPerMinuteCount { get; set; }

    public string ConsensusContractAddress { get; set; }
    public string TreasuryContractAddress { get; set; }

    public long BlockHeightCacheExpiration { get; set; }
    public Dictionary<string, Dictionary<string, string>> ContractNames { get; set; }
    public Dictionary<string, Dictionary<string, string>> BPNames { get; set; }
    public Dictionary<string, int> FilterTypes { get; set; }


    //key is contract address, value is method Name
    public Dictionary<string, HashSet<string>> ContractParseLogEvent { get; set; } = new();

    public bool ParseLogEvent(string address, string method)
    {
        return ContractParseLogEvent.TryGetValue(address, out var methodSet) && methodSet.Contains(method);
    }
}