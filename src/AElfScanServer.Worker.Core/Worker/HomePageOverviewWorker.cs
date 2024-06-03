using System.Threading.Tasks;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.HttpApi.DataStrategy;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.DataStrategy;
using AElfScanServer.Options;
using AElfScanServer.Worker.Core.Options;
using AElfScanServer.Worker.Core.Provider;
using AElfScanServer.Worker.Core.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core.Worker;

public class HomePageOverviewWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<HomePageOverviewWorker> _logger;
    private readonly IOptionsMonitor<PullTransactionChainIdsOptions> _workerOptions;

    private readonly DataStrategyContext<string, HomeOverviewResponseDto> _overviewDataStrategy;

    public HomePageOverviewWorker(AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<HomePageOverviewWorker> logger,
        OverviewDataStrategy overviewDataStrategy,
        IOptionsMonitor<PullTransactionChainIdsOptions> workerOptions
    ) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        timer.Period = 1000 * 2;
        _overviewDataStrategy = new DataStrategyContext<string, HomeOverviewResponseDto>(overviewDataStrategy);
        _workerOptions = workerOptions;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        foreach (var chainId in _workerOptions.CurrentValue.ChainIds)
        {
            _logger.LogInformation("Start to load home page overview data for chain {0}", chainId);
            await _overviewDataStrategy.LoadData(chainId);
        }
    }
}