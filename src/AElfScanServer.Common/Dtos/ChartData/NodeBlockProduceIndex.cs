using System;
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

public class DailyAvgTransactionFeeIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }

    [Keyword] public string AvgFeeUsdt { get; set; }

    [Keyword] public string AvgFeeElf { get; set; }

    public string TotalFeeElf { get; set; }
    public int TransactionCount { get; set; }

    [Keyword] public string DateStr { get; set; }
}

public class DailyAvgBlockSizeIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }

    [Keyword] public string AvgSize { get; set; }


    [Keyword] public string DateStr { get; set; }
}

public class DailyBlockRewardIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }

    [Keyword] public string BlockReward { get; set; }


    [Keyword] public string DateStr { get; set; }
    public long TotalBlockCount { get; set; }
}

public class DailyTotalBurntIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }

    [Keyword] public string Burnt { get; set; }

    public int HasBurntBlockCount { get; set; }
    [Keyword] public string DateStr { get; set; }
}

public class DailyDeployContractIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }

    public int Count { get; set; }


    [Keyword] public string DateStr { get; set; }
}

public class ElfPriceIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return DateStr; }
    }

    public long OpenTime { get; set; }
    [Keyword] public string DateStr { get; set; }
    [Keyword] public string Open { get; set; }
    [Keyword] public string High { get; set; }
    [Keyword] public string Low { get; set; }

    [Keyword] public string Close { get; set; }
    // public string Volume { get; set; }
    // public long CloseTime { get; set; }
    // public string QuoteAssetVolume { get; set; }
    // public int NumberOfTrades { get; set; }
    // public string TakerBuyBaseAssetVolume { get; set; }
    // public string TakerBuyQuoteAssetVolume { get; set; }
    // public string Ignore { get; set; }
}

public class DailyTransactionCountIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }
    public int TransactionCount { get; set; }
    public int BlockCount { get; set; }


    [Keyword] public string DateStr { get; set; }
}

public class DailyUniqueAddressCountIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }
    public int AddressCount { get; set; }

    public int TotalUniqueAddressees { get; set; }


    [Keyword] public string DateStr { get; set; }
}

public class DailyActiveAddressCountIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return Date + "_" + ChainId; }
    }

    public long Date { get; set; }

    [Keyword] public string ChainId { get; set; }
    public long AddressCount { get; set; }

    public long SendAddressCount { get; set; }
    public long ReceiveAddressCount { get; set; }

    [Keyword] public string DateStr { get; set; }
}

public class DailyJobExecuteIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return DateStr + "_" + ChainId; }
    }

    [Keyword] public string ChainId { get; set; }
    [Keyword] public string DateStr { get; set; }
    public bool IsStatistic { get; set; }
    public DateTime StatisticStartTime { get; set; }
    public double CostTime { get; set; }

    public DateTime DataWriteFinishTime { get; set; }
}