using System;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Token.HttpApi.Dtos.Input;
using AElfScanServer.Token.HttpApi.Options;
using AElfScanServer.Token.HttpApi.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Common.Token.HttpApi.Worker;

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
        var tasks = _workerOptions.CurrentValue.ChainInfos.Select(info => UpdateTokenHolderCount(info.ChainId));
        await Task.WhenAll(tasks);
    }

    private async Task UpdateTokenHolderCount(string chainId)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        _logger.LogInformation("Update TokenHolderCount date {date}", today);
        var exist = await _tokenHolderPercentProvider.CheckExistAsync(chainId, today);
        if (exist)
        {
            _logger.LogInformation("Update TokenHolderCount date {date} exist.", today);
            return;
        }
            
        var batchSize = _workerOptions.CurrentValue.BatchSize;
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