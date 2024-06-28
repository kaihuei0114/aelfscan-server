namespace AElfScanServer.Common.Dtos.ChartData;

public class DailyTransactionCount
{
    public long Date { get; set; }
    public int TransactionCount { get; set; }
    public int BlockCount { get; set; }
}

public class UniqueAddressCount
{
    public long Date { get; set; }
    public int AddressCount { get; set; }

    public int TotalUniqueAddressees { get; set; }
}

public class DailyActiveAddressCount
{
    public long Date { get; set; }
    public long AddressCount { get; set; }

    public long SendAddressCount { get; set; }
    public long ReceiveAddressCount { get; set; }
}

public class DailyBlockProduceCount
{
    public long Date { get; set; }
    public string BlockProductionRate { get; set; }
    public long BlockCount { get; set; }
    public long MissedBlockCount { get; set; }
}

public class DailyBlockProduceDuration
{
    public long Date { get; set; }
    public string AvgBlockDuration { get; set; }
    public string LongestBlockDuration { get; set; }
    public string ShortestBlockDuration { get; set; }
}

public class DailyCycleCount
{
    public long Date { get; set; }
    public long CycleCount { get; set; }
    public long MissedBlockCount { get; set; }
    public long MissedCycle { get; set; }
}