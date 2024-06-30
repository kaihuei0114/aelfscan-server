using System.Collections.Generic;
using AElf.EntityMapping.Entities;
using AElfScanServer.Domain.Common.Entities;
using AElfScanServer.Domain.Shared.Common;
using Nest;

namespace AElfScanServer.Common.Dtos.ChartData;

public class NodeBlockProduceIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return NodeAddress + "_" + RoundNumber + "_" + ChainId; }
    }

    public long RoundNumber { get; set; }

    public long DurationSeconds { get; set; }

    [Keyword] public string ChainId { get; set; }
    public long Blcoks { get; set; }

    public long MissedBlocks { get; set; }


    public long StartTime { get; set; }

    public long EndTime { get; set; }

    public bool IsExtraBlockProducer { get; set; }

    [Keyword] public string NodeAddress { get; set; }
}

public class RoundIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return RoundNumber + "_" + ChainId; }
    }

    public long RoundNumber { get; set; }
    [Keyword] public string ChainId { get; set; }

    public long DurationSeconds { get; set; }

    [Keyword] public string DateStr { get; set; }

    public int ProduceBlockBpCount { get; set; }

    public int NotProduceBlockBpCount { get; set; }

    public List<string> ProduceBlockBpAddresses { get; set; }

    public List<string> NotProduceBlockBpAddresses { get; set; }

    public long Blcoks { get; set; }

    public long MissedBlocks { get; set; }

    public long StartTime { get; set; }

    public long EndTime { get; set; }
}

public class HourNodeBlockProduceIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId + "_" + NodeAddress; }
    }

    public long Date { get; set; }
    [Keyword] public string ChainId { get; set; }
    [Keyword] public string DateStr { get; set; }
    public long TotalCycle { get; set; }

    public int Hour { get; set; }

    public long Blocks { get; set; }

    public long MissedBlocks { get; set; }


    [Keyword] public string NodeAddress { get; set; }
}

public class DailyBlockProduceCountIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }


    [Keyword] public string BlockProductionRate { get; set; }

    public long BlockCount { get; set; }

    public long MissedBlockCount { get; set; }
}

public class DailyBlockProduceDurationIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    [Keyword] public string ChainId { get; set; }

    public long Date { get; set; }
    [Keyword] public string AvgBlockDuration { get; set; }
    [Keyword] public string LongestBlockDuration { get; set; }
    [Keyword] public string ShortestBlockDuration { get; set; }
}

public class DailyCycleCountIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    [Keyword] public string ChainId { get; set; }
    public long Date { get; set; }
    public long CycleCount { get; set; }
    public long MissedBlockCount { get; set; }
    public long MissedCycle { get; set; }
}