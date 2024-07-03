using System.Collections.Generic;
using AElfScanServer.Common.Dtos.ChartData;

namespace AElfScanServer.HttpApi.Dtos.ChartData;

public class UniqueAddressCountResp
{
    public long Total { get; set; }
    public List<DailyUniqueAddressCount> List { get; set; }
    public DailyUniqueAddressCount HighestIncrease { get; set; }
    public DailyUniqueAddressCount LowestIncrease { get; set; }
    public string ChainId { get; set; }
}

public class ActiveAddressCountResp
{
    public long Total { get; set; }
    public List<DailyActiveAddressCount> List { get; set; }
    public DailyActiveAddressCount HighestActiveCount { get; set; }
    public DailyActiveAddressCount LowestActiveCount { get; set; }
    public string ChainId { get; set; }
}

public class BlockProduceRateResp
{
    public long Total { get; set; }
    public DailyBlockProduceCount HighestBlockProductionRate { get; set; }
    public DailyBlockProduceCount lowestBlockProductionRate { get; set; }
    public List<DailyBlockProduceCount> List { get; set; }
}

public class AvgBlockDurationResp
{
    public long Total { get; set; }
    public DailyBlockProduceDuration HighestAvgBlockDuration { get; set; }
    public DailyBlockProduceDuration LowestBlockProductionRate { get; set; }
    public List<DailyBlockProduceDuration> List { get; set; }
}

public class CycleCountResp
{
    public long Total { get; set; }

    public DailyCycleCount HighestMissedCycle { get; set; }
    public List<DailyCycleCount> List { get; set; }
}

public class NodeBlockProduceResp
{
    public long Total { get; set; }
    public List<NodeBlockProduce> List { get; set; }
    public int TotalCycle { get; set; }
}

public class NodeBlockProduce
{
    public int Total { get; set; }

    public long TotalCycle { get; set; }

    public long DurationSeconds { get; set; }

    public long Blocks { get; set; }

    public long MissedBlocks { get; set; }

    public string BlocksRate { get; set; }


    public long InRound { get; set; }

    public string CycleRate { get; set; }

    public string NodeName { get; set; }

    public string NodeAddress { get; set; }
}