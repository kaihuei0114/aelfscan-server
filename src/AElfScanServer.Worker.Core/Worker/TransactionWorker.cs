using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Worker.Core.Dtos;
using AElfScanServer.Worker.Core.Options;
using AElfScanServer.Worker.Core.Provider;
using AElfScanServer.Worker.Core.Service;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Nito.AsyncEx;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core.Worker;

public class TransactionWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<TransactionWorker> _logger;
    private readonly AELFIndexerOptions _aelfIndexerOptions;
    private readonly ITransactionService _transactionService;
    private readonly WorkerOptions _workerOptions;

    private bool TransactionStartBlockHeightHasUsed = false;
    private bool WorkerFirstStart = true;
    private readonly AELFIndexerProvider _aelfIndexerProvider;

    public TransactionWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionWorker> logger, ITransactionService transactionService,
        IOptionsSnapshot<AELFIndexerOptions> aelfIndexerOptions, IStorageProvider storageProvider,
        IOptionsSnapshot<WorkerOptions> workerOptions, AELFIndexerProvider aelfIndexerProvider
    ) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _storageProvider = storageProvider;
        _transactionService = transactionService;
        _aelfIndexerOptions = aelfIndexerOptions.Value;
        timer.Period = TransactionWorkerOptions.TimePeriod;
        _workerOptions = workerOptions.Value;
        _aelfIndexerProvider = aelfIndexerProvider;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // do something
        // await _transactionService.PullTokenData();

        var tasks = _workerOptions.PullDataChainIds.Select(ExecutePullTransactionAsync);
        await Task.WhenAll(tasks);
    }

    private async Task ExecutePullTransactionAsync(string chainId)
    {
        try
        {
            var summariesAsync = await _aelfIndexerProvider.GetLatestSummariesAsync(chainId);
            var startBlockHeight = 0l;

            if (_workerOptions.TransactionStartBlockHeightSwitch && !TransactionStartBlockHeightHasUsed &&
                _workerOptions.TransactionStartBlockHeight > 0)
            {
                startBlockHeight = _workerOptions.TransactionStartBlockHeight;
                TransactionStartBlockHeightHasUsed = true;
            }
            else if (WorkerFirstStart)
            {
                startBlockHeight = await _transactionService.GetLastBlockHeight(chainId);

      
                var start = summariesAsync.First().LatestBlockHeight - 10;
                startBlockHeight = startBlockHeight > start ? startBlockHeight : start;

                WorkerFirstStart = false;
            }
            else
            {
                var latestBlockHeight =
                    await _storageProvider.GetAsync<SearchHeightDto>(RedisKeyHelper.PullBlockHeight(chainId));
                startBlockHeight = latestBlockHeight?.BlockHeight ?? 0;
            }


            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var tasks = new List<Task>();

            var nextBlockHeight = 0l;
            for (long i = startBlockHeight;
                 i <= startBlockHeight + _aelfIndexerOptions.PullHeightInterval &&
                 i <= summariesAsync.First().LatestBlockHeight;
                 i += 1000)
            {
                var tmpEnd = i + 1000 > summariesAsync.First().LatestBlockHeight
                    ? summariesAsync.First().LatestBlockHeight
                    : i + 1000;
                _logger.LogInformation("Start handler transaction blockRange:[{0},{1}],chainId:{2}", i, tmpEnd,
                    chainId);
                var t = _transactionService.HandlerTransactionAsync(chainId, i,
                    tmpEnd);
                nextBlockHeight = tmpEnd;
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);


            _logger.LogInformation("cost time seconds:{0}", stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("next start block height:{0}", nextBlockHeight);
            await _storageProvider.SetAsync(RedisKeyHelper.PullBlockHeight(chainId),
                new SearchHeightDto { BlockHeight = nextBlockHeight });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "pull transaction error:{e}", e.Message);
        }
    }
}