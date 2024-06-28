using System.Threading.Tasks;
using AElfScanServer.Worker.Core.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core.Worker;

public class NetworkStatisticWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ITransactionService _transactionService;

    private readonly ILogger<NetworkStatisticWorker> _logger;


    public NetworkStatisticWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<NetworkStatisticWorker> logger, ITransactionService transactionService) : base(timer,
        serviceScopeFactory)
    {
        timer.Period = 1000 * 1;
        _logger = logger;
        _transactionService = transactionService;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await _transactionService.UpdateNetwork();
    }
}