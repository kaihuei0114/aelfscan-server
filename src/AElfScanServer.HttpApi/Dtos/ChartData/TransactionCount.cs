using System.Collections.Generic;
using AElfScanServer.Common.Dtos.ChartData;

namespace AElfScanServer.HttpApi.Dtos.ChartData;

public class ChartDataRequest
{
    public long StartDate { get; set; } = 0;
    public long EndDate { get; set; } = 0;
    public string ChainId { get; set; }
}

public class SetRoundRequest
{
    public long RoundNumber { get; set; }
    public string ChainId { get; set; }

    public bool SetNumber { get; set; }
}

public class DailyTransactionCountResp
{
    public List<DailyTransactionCount> List { get; set; }

    public DailyTransactionCount HighestTransactionCount { get; set; }
    public DailyTransactionCount LowesTransactionCount { get; set; }
    public string ChainId { get; set; }
}