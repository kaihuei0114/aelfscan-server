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

public class BlockProduceRateResp
{
    public DailyBlockProduceCount HighestBlockProductionRate { get; set; }
    public DailyBlockProduceCount lowestBlockProductionRate { get; set; }
    public List<DailyBlockProduceCount> List { get; set; }
}

public class AvgBlockDurationResp
{
    public DailyBlockProduceDuration HighestAvgBlockDuration { get; set; }
    public DailyBlockProduceDuration LowestBlockProductionRate { get; set; }
    public List<DailyBlockProduceDuration> List { get; set; }
}

public class CycleCountResp
{
    public DailyCycleCount HighestMissedCycle { get; set; }
    public List<DailyCycleCount> List { get; set; }
}

public class NodeBlockProduceResp
{
    public List<NodeBlockProduce> List { get; set; }
}

public class NodeBlockProduce
{
    public long TotalCycle { get; set; }

    public long DurationSeconds { get; set; }

    public long Blocks { get; set; }

    public long MissedBlocks { get; set; }

    public string BlocksRate { get; set; }

    public long MissedCycle { get; set; }

    public string CycleRate { get; set; }

    public string NodeName { get; set; }

    public string NodeAddress { get; set; }
}