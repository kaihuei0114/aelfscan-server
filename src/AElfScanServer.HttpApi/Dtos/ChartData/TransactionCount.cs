using System.Collections.Generic;
using AElfScanServer.Common.Dtos.ChartData;

namespace AElfScanServer.HttpApi.Dtos.ChartData;

public class ChartDataRequest
{
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string ChainId { get; set; }
}

public class DailyTransactionCountResp
{
    public List<DailyTransactionCount> List { get; set; }

    public DailyTransactionCount HighestTransactionCount { get; set; }
    public DailyTransactionCount LowesTransactionCount { get; set; }
    public string ChainId { get; set; }
}