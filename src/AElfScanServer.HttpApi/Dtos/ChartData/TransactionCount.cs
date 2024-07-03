using System.Collections.Generic;
using System.Linq;
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
    public string ChainId { get; set; }

    public string StartDate { get; set; }
    public string EndDate { get; set; }

    public int SetNumber { get; set; }

    public bool UpdateData { get; set; }
}

public class InitRoundResp
{
    public long MinRound { get; set; }

    public long MaxRound { get; set; }

    public long RoundCount { get; set; }

    public string MinDate { get; set; }

    public List<string> UpdateDate { get; set; }


    public List<string> UpdateDateNodeBlockProduce { get; set; }

    public string MaxDate { get; set; }
    public string FinishDate { get; set; }
}

public class DailyTransactionCountResp
{
    public long Total { get; set; }
    public List<DailyTransactionCount> List { get; set; }

    public DailyTransactionCount HighestTransactionCount { get; set; }
    public DailyTransactionCount LowesTransactionCount { get; set; }
    public string ChainId { get; set; }
}

public class DailyAvgTransactionFeeResp
{
    public long Total { get; set; }
    public List<DailyAvgTransactionFee> List { get; set; }

    public DailyAvgTransactionFee Highest { get; set; }
    public DailyAvgTransactionFee Lowest { get; set; }
    public string ChainId { get; set; }
}

public class DailyAvgTransactionFee
{
    public long Date { get; set; }

    public string AvgFeeUsdt { get; set; }

    public string AvgFeeElf { get; set; }

    public string TotalFeeElf { get; set; }
    public int TransactionCount { get; set; }

    public string DateStr { get; set; }
}

public class DailyBlockRewardResp
{
    public long Total { get; set; }
    public List<DailyBlockReward> List { get; set; }

    public DailyBlockReward Highest { get; set; }
    public DailyBlockReward Lowest { get; set; }
    public string ChainId { get; set; }
}

public class DailyBlockReward
{
    public long Date { get; set; }
    public string ChainId { get; set; }
    public string BlockReward { get; set; }
    public string DateStr { get; set; }
    public long TotalBlockCount { get; set; }
}

public class DailyTotalBurntResp
{
    public long Total { get; set; }
    public List<DailyTotalBurnt> List { get; set; }

    public DailyTotalBurnt Highest { get; set; }
    public DailyTotalBurnt Lowest { get; set; }
    public string ChainId { get; set; }
}

public class DailyTotalBurnt
{
    public long Date { get; set; }

    public string Burnt { get; set; }

    public int HasBurntBlockCount { get; set; }
    public string DateStr { get; set; }
}

public class DailyDeployContractResp
{
    public long Total { get; set; }
    public List<DailyDeployContract> List { get; set; }

    public DailyDeployContract Highest { get; set; }
    public DailyDeployContract Lowest { get; set; }
    public string ChainId { get; set; }
}

public class DailyDeployContract
{
    public long Date { get; set; }

    public string Count { get; set; }

    public int HasBurntBlockCount { get; set; }
    public string DateStr { get; set; }
}

public class ElfPriceIndexResp
{
    public long Total { get; set; }
    public List<ElfPrice> List { get; set; }

    public ElfPrice Highest { get; set; }
    public ElfPrice Lowest { get; set; }
    public string ChainId { get; set; }
}

public class ElfPrice
{
    public long Date { get; set; }

    public string DateStr { get; set; }
    public string Open { get; set; }
    public string High { get; set; }
    public string Low { get; set; }
    public string Price { get; set; }
}