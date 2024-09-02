using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Provider;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.IndexerPluginProvider;
using AElfScanServer.Common.Options;
using AElfScanServer.DataStrategy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching;

namespace AElfScanServer.HttpApi.DataStrategy;

public class OverviewDataStrategy : DataStrategyBase<string, HomeOverviewResponseDto>
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly HomePageProvider _homePageProvider;
    private readonly BlockChainDataProvider _blockChainProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IEntityMappingRepository<DailyUniqueAddressCountIndex, string> _uniqueAddressRepository;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;


    public OverviewDataStrategy(IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<GlobalOptions> globalOptions,
        AELFIndexerProvider aelfIndexerProvider,
        HomePageProvider homePageProvider,
        BlockChainDataProvider blockChainProvider,
        ITokenIndexerProvider tokenIndexerProvider,
        IBlockChainIndexerProvider blockChainIndexerProvider,
        IEntityMappingRepository<DailyUniqueAddressCountIndex, string> uniqueAddressRepository,
        ILogger<DataStrategyBase<string, HomeOverviewResponseDto>> logger, IDistributedCache<string> cache) : base(
        optionsAccessor, logger, cache)
    {
        _globalOptions = globalOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
        _homePageProvider = homePageProvider;
        _blockChainProvider = blockChainProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _uniqueAddressRepository = uniqueAddressRepository;
    }

    public override async Task<HomeOverviewResponseDto> QueryData(string chainId)
    {
        DataStrategyLogger.LogInformation("GetBlockchainOverviewAsync:{chainId}", chainId);
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


            tasks.Add(_uniqueAddressRepository.GetQueryableAsync().ContinueWith(
                task =>
                {
                    overviewResp.Accounts =
                        task.Result.Where(c => c.ChainId == chainId).OrderByDescending(c => c.Date).Take(1).ToList()
                            .First().TotalUniqueAddressees;
                }));


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
                task => { overviewResp.Tps = (task.Result / 60).ToString("F2"); }));

            await Task.WhenAll(tasks);


            DataStrategyLogger.LogInformation("Set home page overview success:{chainId}", chainId);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "get home page overview err,chainId:{chainId}", chainId);
        }

        return overviewResp;
    }


    public override string DisplayKey(string chainId)
    {
        return RedisKeyHelper.HomeOverview(chainId);
    }
}