using System;
using System.Collections.Generic;
using System.Linq;

namespace AElfScanServer.Options;

public class GlobalOptions
{
    public const string PrivateKey = "09da44778f8db2e602fb484334f37df19e221c84c4582ce5b7770ccfbc3ddbef";
    public int MaxResultCount { get; set; }

    public Dictionary<string, List<string>> BurntFeeContractAddresses { get; set; }
    public List<string> ChainIds { get; set; }

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

    public long RewardCacheExpiration { get; set; } = 10;
    public long TransactionPerMinuteCount { get; set; }

    public string ConsensusContractAddress { get; set; }
    public string TreasuryContractAddress { get; set; }

    public long BlockHeightCacheExpiration { get; set; }
    public Dictionary<string, Dictionary<string, string>> ContractNames { get; set; }
    public Dictionary<string, Dictionary<string, string>> BPNames { get; set; }
    public Dictionary<string, int> FilterTypes { get; set; }

    public int TransactionListMaxCount { get; set; }


    public Dictionary<string, HashSet<string>> ContractParseLogEvent { get; set; } = new();

    public bool ParseLogEvent(string address, string method)
    {
        return ContractParseLogEvent.TryGetValue(address, out var methodSet) && methodSet.Contains(method);
    }

    public string GetContractName(string chainId, string address)
    {
        if (!ContractNames.TryGetValue(chainId, out var contractNames))
        {
            return null;
        }

        return contractNames.TryGetValue(address, out var name) ? name : null;
    }

    public Dictionary<string, string> GetContractNameDict(string chainId, string keyword, bool exactMatch = false)
    {
        if (!ContractNames.TryGetValue(chainId, out var contractNames))
        {
            return new();
        }

        var filteredContractNames = contractNames
            .Where(kv => IsMatch(kv.Value, keyword, exactMatch))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return filteredContractNames;
    }


    private static bool IsMatch(string value, string keyword, bool exactMatch)
    {
        if (exactMatch)
        {
            return value == keyword;
        }

        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}