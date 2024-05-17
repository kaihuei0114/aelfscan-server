using System.Collections.Generic;

namespace AElfScanServer.Worker.Core.Options;

public class WorkerOptions
{
    public List<ChainOptionDto> Chains { get; set; }

    public long TransactionStartBlockHeight { get; set; }

    public bool TransactionStartBlockHeightSwitch { get; set; }

    public bool ClearTransactionDataSwitch { get; set; }

    public List<string> EsUrl { get; set; }

    public List<string> PullDataChainIds { get; set; }
}

public class ChainOptionDto
{
    public string ChainId { get; set; }
    public string BasicInfoUrl { get; set; }
}