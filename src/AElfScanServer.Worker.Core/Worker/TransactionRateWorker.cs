using System.Threading.Tasks;
using AElfScanServer.Worker.Core.Service;
using AElfScanServer.Worker.Core.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core;

public class TransactionRateWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ITransactionService _transactionService;

    private readonly ILogger<TransactionWorker> _logger;

    public TransactionRateWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionWorker> logger, ITransactionService transactionService) : base(timer,
        serviceScopeFactory)
    {
        timer.Period = 3000;
        _logger = logger;
        _transactionService = transactionService;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // do something
        // await _transactionService.UpdateTransactionRateAsync();
        // Thread.Sleep(1000000000);
        // _logger.LogInformation("PullTransactions start");
    }
}