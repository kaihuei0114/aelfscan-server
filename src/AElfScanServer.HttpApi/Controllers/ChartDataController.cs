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

    // [HttpPost("setRound")]
    // public async Task<string> SetRoundAsync(SetRoundRequest request)
    // {
    //     return await _chartDataService.SetRoundNumberAsync(request);
    // }


    [HttpGet("initRound")]
    public async Task<InitRoundResp> SetRoundAsync(SetRoundRequest request)
    {
        return await _chartDataService.InitDailyNetwork(request);
    }
}