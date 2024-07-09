using System.Threading.Tasks;
using AElfScanServer.HttpApi.Dtos.ChartData;
using AElfScanServer.HttpApi.Service;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace AElfScanServer.HttpApi.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("statistics")]
[Route("api/app/statistics")]
public class ChartDataController : AbpController
{
    private readonly IChartDataService _chartDataService;

    public ChartDataController(IChartDataService chartDataService)
    {
        _chartDataService = chartDataService;
    }

    [HttpGet("dailyTransactions")]
    public async Task<DailyTransactionCountResp> GetDailyTransactionCountAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyTransactionCountAsync(request);
    }

    [HttpGet("uniqueAddresses")]
    public async Task<UniqueAddressCountResp> GetUniqueAddressesCountAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetUniqueAddressCountAsync(request);
    }


    [HttpGet("dailyActiveAddresses")]
    public async Task<ActiveAddressCountResp> GetActiveAddressesCountAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetActiveAddressCountAsync(request);
    }


    [HttpGet("blockProduceRate")]
    public async Task<BlockProduceRateResp> GetBlockProduceRateAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetBlockProduceRateAsync(request);
    }

    [HttpGet("avgBlockDuration")]
    public async Task<AvgBlockDurationResp> GetAvgBlockDurationRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetAvgBlockDurationRespAsync(request);
    }

    [HttpGet("cycleCount")]
    public async Task<CycleCountResp> GetCycleCountRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetCycleCountRespAsync(request);
    }


    [HttpGet("nodeBlockProduce")]
    public async Task<NodeBlockProduceResp> GetBlockProduceRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetNodeBlockProduceRespAsync(request);
    }


    [HttpGet("dailyAvgTransactionFee")]
    public async Task<DailyAvgTransactionFeeResp> GetDailyAvgTransactionFeeRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyAvgTransactionFeeRespAsync(request);
    }

    [HttpGet("dailyTxFee")]
    public async Task<DailyTransactionFeeResp> GetTransactionFeeRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyTransactionFeeRespAsync(request);
    }


    [HttpGet("dailyTotalBurnt")]
    public async Task<DailyTotalBurntResp> GetDailyTotalBurntRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyTotalBurntRespAsync(request);
    }


    [HttpGet("dailyElfPrice")]
    public async Task<ElfPriceIndexResp> GetElfPriceIndexRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetElfPriceIndexRespAsync(request);
    }


    [HttpGet("dailyDeployContract")]
    public async Task<DailyDeployContractResp> GetDailyDeployContractRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyDeployContractRespAsync(request);
    }


    [HttpGet("dailyBlockReward")]
    public async Task<DailyBlockRewardResp> GetDailyBlockRewardRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyBlockRewardRespAsync(request);
    }


    [HttpGet("dailyAvgBlockSize")]
    public async Task<DailyAvgBlockSizeResp> GetDailyAvgBlockSizeRespRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyAvgBlockSizeRespRespAsync(request);
    }


    [HttpGet("topContractCall")]
    public async Task<TopContractCallResp> GetTopContractCallRespRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetTopContractCallRespRespAsync(request);
    }


    [HttpGet("dailyContractCall")]
    public async Task<DailyTotalContractCallResp> GetDailyTotalContractCallRespRespAsync(ChartDataRequest request)
    {
        return await _chartDataService.GetDailyTotalContractCallRespRespAsync(request);
    }


    [HttpGet("initRound")]
    public async Task<InitRoundResp> SetRoundAsync(SetRoundRequest request)
    {
        return await _chartDataService.InitDailyNetwork(request);
    }


    [HttpGet("getJob")]
    public async Task<JonInfoResp> GetJobInfo(SetJob request)
    {
        return await _chartDataService.GetJobInfo(request);
    }
}