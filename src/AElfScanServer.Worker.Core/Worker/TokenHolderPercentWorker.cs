using System;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Common.Dtos.Input;
using AElfScanServer.Common.IndexerPluginProvider;
using AElfScanServer.HttpApi.Provider;
using AElfScanServer.Worker.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Worker.Core.Worker;

public class TokenHolderPercentWorker : AsyncPeriodicBackgroundWorkerBase
{
    private const string WorkerKey = "TokenHolderPercentWorker";
    private readonly ILogger<TokenHolderPercentWorker> _logger;
    private readonly ITokenHolderPercentProvider _tokenHolderPercentProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptions;

    public TokenHolderPercentWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<TokenHolderPercentWorker> logger, ITokenHolderPercentProvider tokenHolderPercentProvider,
        ITokenIndexerProvider tokenIndexerProvider, IOptionsMonitor<WorkerOptions> workerOptionsMonitor)
        : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _tokenHolderPercentProvider = tokenHolderPercentProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _workerOptions = workerOptionsMonitor;
        timer.Period = _workerOptions.CurrentValue.GetWorkerPeriodMinutes(WorkerKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tasks = _workerOptions.CurrentValue.Chains.Select(info => UpdateTokenHolderCount(info.ChainId));
        await Task.WhenAll(tasks);
    }

    private async Task UpdateTokenHolderCount(string chainId)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        _logger.LogInformation("Update TokenHolderCount date {dateStr}", today);
        var exist = await _tokenHolderPercentProvider.CheckExistAsync(chainId, today);
        if (exist)
        {
            _logger.LogInformation("Update TokenHolderCount date {dateStr} exist.", today);
            return;
        }

        var batchSize = 1000;
        var skipCount = 0;
        var moreData = true;
        while (moreData)
        {
            var input = new TokenListInput()
            {
                ChainId = chainId,
                SkipCount = skipCount,
                MaxResultCount = batchSize
            };

            var tokenListDto = await _tokenIndexerProvider.GetTokenListAsync(input);

            if (!tokenListDto.Items.Any() || tokenListDto.Items.Count < batchSize)
            {
                moreData = false;
            }
            else
            {
                skipCount += batchSize;
            }

            var dictionary = tokenListDto.Items.ToDictionary(token => token.Symbol, token => token.HolderCount);
            await _tokenHolderPercentProvider.UpdateTokenHolderCount(dictionary, chainId);
        }
    }
}