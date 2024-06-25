using System.Collections.Generic;
using AElfScanServer.Common.Dtos.ChartData;

namespace AElfScanServer.HttpApi.Dtos.ChartData;

public class UniqueAddressCountResp
{
    public List<UniqueAddressCount> List { get; set; }
    public UniqueAddressCount HighestIncrease { get; set; }
    public UniqueAddressCount LowestIncrease { get; set; }
    public string ChainId { get; set; }
}




public class ActiveAddressCountResp
{
    public List<DailyActiveAddressCount> List { get; set; }
    public DailyActiveAddressCount HighestActiveCount { get; set; }
    public DailyActiveAddressCount LowestActiveCount { get; set; }
    public string ChainId { get; set; }
}