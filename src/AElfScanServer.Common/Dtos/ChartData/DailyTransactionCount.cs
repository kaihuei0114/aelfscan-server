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
}

public class DailyActiveAddressCount
{
    public long Date { get; set; }
    public long AddressCount { get; set; }

    public long SendAddressCount { get; set; }
    public long ReceiveAddressCount { get; set; }
}