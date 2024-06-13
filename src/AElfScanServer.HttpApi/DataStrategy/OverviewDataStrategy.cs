using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Provider;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.IndexerPluginProvider;
using AElfScanServer.Common.Options;
using AElfScanServer.DataStrategy;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AElfScanServer.HttpApi.DataStrategy;

public class OverviewDataStrategy : DataStrategyBase<string, HomeOverviewResponseDto>
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly HomePageProvider _homePageProvider;
    private readonly BlockChainDataProvider _blockChainProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;


    public OverviewDataStrategy(IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<GlobalOptions> globalOptions,
        AELFIndexerProvider aelfIndexerProvider,
        HomePageProvider homePageProvider,
        BlockChainDataProvider blockChainProvider,
        ITokenIndexerProvider tokenIndexerProvider,
        IBlockChainIndexerProvider blockChainIndexerProvider,
        ILogger<DataStrategyBase<string, HomeOverviewResponseDto>> logger) : base(optionsAccessor, logger)
    {
        _globalOptions = globalOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
        _homePageProvider = homePageProvider;
        _blockChainProvider = blockChainProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
    }

    public override async Task<HomeOverviewResponseDto> QueryData(string chainId)
    {
        DataStrategyLogger.LogInformation("GetBlockchainOverviewAsync:{c}", chainId);
        var overviewResp = new HomeOverviewResponseDto();
        try
        {
            var tasks = new List<Task>();
            tasks.Add(_aelfIndexerProvider.GetLatestBlockHeightAsync(chainId).ContinueWith(
                task => { overviewResp.BlockHeight = task.Result; }));

            tasks.Add(_blockChainIndexerProvider.GetTransactionCount(chainId).ContinueWith(task =>
            {
                overviewResp.Transactions = task.Result;
            }));

            tasks.Add(_tokenIndexerProvider.GetAccountCountAsync(chainId).ContinueWith(
                task => { overviewResp.Accounts = task.Result; }));


            tasks.Add(_homePageProvider.GetRewardAsync(chainId).ContinueWith(
                task =>
                {
                    overviewResp.Reward = task.Result.ToDecimalsString(8);
                    overviewResp.CitizenWelfare = (task.Result * 0.75).ToDecimalsString(8);
                }));

            tasks.Add(_blockChainProvider.GetTokenUsd24ChangeAsync("ELF").ContinueWith(
                task =>
                {
                    overviewResp.TokenPriceRate24h = task.Result.PriceChangePercent;
                    overviewResp.TokenPriceInUsd = task.Result.LastPrice;
                }));
            tasks.Add(_homePageProvider.GetTransactionCountPerLastMinute(chainId).ContinueWith(
                task => { overviewResp.Tps = task.Result; }));

            await Task.WhenAll(tasks);


            DataStrategyLogger.LogInformation("Set home page overview success:{c}", chainId);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "get home page overview err,chainId:{c}", chainId);
        }

        return overviewResp;
    }


    public override string DisplayKey(string chainId)
    {
        return RedisKeyHelper.HomeOverview(chainId);
    }
}