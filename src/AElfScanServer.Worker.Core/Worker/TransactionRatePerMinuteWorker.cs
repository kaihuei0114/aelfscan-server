using System.Threading.Tasks;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.Worker.Core.Service;
using AElfScanServer.Worker.Core.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core;

public class TransactionRatePerMinuteWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ITransactionService _transactionService;

    private readonly ILogger<TransactionRatePerMinuteWorker> _logger;


    public TransactionRatePerMinuteWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionRatePerMinuteWorker> logger, ITransactionService transactionService) : base(timer,
        serviceScopeFactory)
    {
        timer.Period = 1000 * 30;
        _logger = logger;
        _transactionService = transactionService;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await _transactionService.UpdateTransactionRatePerMinuteAsync();

        _logger.LogInformation("Update transaction rate per minute success.");
    }
}