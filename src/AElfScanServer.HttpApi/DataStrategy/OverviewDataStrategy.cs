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
using AElfScanServer.HttpApi.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching;
using AddressIndex = AElfScanServer.Common.Dtos.ChartData.AddressIndex;

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
    private readonly IChartDataService _chartDataService;
    private readonly IEntityMappingRepository<AddressIndex, string> _addressRepository;

    public OverviewDataStrategy(IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<GlobalOptions> globalOptions,
        AELFIndexerProvider aelfIndexerProvider,
        HomePageProvider homePageProvider,
        BlockChainDataProvider blockChainProvider,
        ITokenIndexerProvider tokenIndexerProvider,
        IBlockChainIndexerProvider blockChainIndexerProvider,
        IEntityMappingRepository<DailyUniqueAddressCountIndex, string> uniqueAddressRepository,
        ILogger<DataStrategyBase<string, HomeOverviewResponseDto>> logger, IDistributedCache<string> cache,
        IChartDataService chartDataService, IEntityMappingRepository<AddressIndex, string> addressRepository) : base(
        optionsAccessor, logger, cache)
    {
        _globalOptions = globalOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
        _homePageProvider = homePageProvider;
        _blockChainProvider = blockChainProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _uniqueAddressRepository = uniqueAddressRepository;
        _chartDataService = chartDataService;
        _addressRepository = addressRepository;
    }

    public override async Task<HomeOverviewResponseDto> QueryData(string chainId)
    {
        DataStrategyLogger.LogInformation("GetBlockchainOverviewAsync:{chainId}", chainId);
        var overviewResp = new HomeOverviewResponseDto();

        try
        {
            if (chainId.IsNullOrEmpty())
            {
                var queryMergeChainData = await QueryMergeChainData();
                return queryMergeChainData;
            }

            var tasks = new List<Task>();
            tasks.Add(_aelfIndexerProvider.GetLatestBlockHeightAsync(chainId).ContinueWith(
                task => { overviewResp.BlockHeight = task.Result; }));


            tasks.Add(_blockChainIndexerProvider.GetTransactionCount(chainId).ContinueWith(task =>
            {
                overviewResp.MergeTransactions.Total = task.Result;
            }));


            tasks.Add(_uniqueAddressRepository.GetQueryableAsync().ContinueWith(
                task =>
                {
                    overviewResp.MergeAccounts.Total =
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
                task => { overviewResp.MergeTps.Total = (task.Result / 60).ToString("F2"); }));

            await Task.WhenAll(tasks);


            DataStrategyLogger.LogInformation("Set home page overview success:{chainId}", chainId);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "get home page overview err,chainId:{chainId}", chainId);
        }

        return overviewResp;
    }

    public async Task<string> GetMarketCap()
    {
        var marketCap = await _cache.GetAsync("MarketCap");
        if (marketCap.IsNullOrEmpty())
        {
            try
            {
                var marketCapInfo = await _chartDataService.GetDailyMarketCapRespAsync();
                marketCap = marketCapInfo.List.Last().TotalMarketCap;
                await _cache.SetAsync("MarketCap", marketCap);
            }
            catch (Exception e)
            {
                DataStrategyLogger.LogError(e, "get market cap err");
            }
        }

        return marketCap;
    }


    public async Task<long> GetTotalAccount(string chainId)
    {
        var totalCount = 0;
        try
        {
            var count = await _cache.GetAsync("TotalAccount");

            if (count.IsNullOrEmpty())
            {
                totalCount = _addressRepository.GetQueryableAsync().Result.Where(c => c.ChainId == chainId).Count();
                await _cache.SetAsync("TotalAccount", totalCount.ToString());
                return totalCount;
            }

            return long.Parse(count);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "get total account err");
        }

        return totalCount;
    }

    public async Task<HomeOverviewResponseDto> QueryMergeChainData()
    {
        var overviewResp = new HomeOverviewResponseDto();
        try
        {
            decimal mainChainTps = 0;
            decimal sideChainTps = 0;


            var tasks = new List<Task>();


            tasks.Add(_blockChainIndexerProvider.GetTransactionCount("AELF").ContinueWith(task =>
            {
                overviewResp.MergeTransactions.MainChain = task.Result;
            }));


            tasks.Add(_blockChainIndexerProvider.GetTransactionCount(_globalOptions.CurrentValue.SideChainId)
                .ContinueWith(task => { overviewResp.MergeTransactions.SideChain = task.Result; }));


            tasks.Add(_blockChainProvider.GetTokenUsd24ChangeAsync("ELF").ContinueWith(
                task =>
                {
                    overviewResp.TokenPriceRate24h = task.Result.PriceChangePercent;
                    overviewResp.TokenPriceInUsd = task.Result.LastPrice;
                }));

            tasks.Add(_homePageProvider.GetTransactionCountPerLastMinute("AELF").ContinueWith(
                task => { overviewResp.MergeTps.MainChain = (task.Result / 60).ToString("F2"); }));

            tasks.Add(_homePageProvider.GetTransactionCountPerLastMinute(_globalOptions.CurrentValue.SideChainId)
                .ContinueWith(
                    task => { overviewResp.MergeTps.SideChain = (task.Result / 60).ToString("F2"); }));

            tasks.Add(GetMarketCap().ContinueWith(task => { overviewResp.MarketCap = task.Result; }));

            tasks.Add(GetTotalAccount("AELF").ContinueWith(task =>
            {
                overviewResp.MergeAccounts.MainChain = task.Result;
            }));

            tasks.Add(GetTotalAccount(_globalOptions.CurrentValue.SideChainId).ContinueWith(task =>
            {
                overviewResp.MergeAccounts.SideChain = task.Result;
            }));

            await Task.WhenAll(tasks);
            overviewResp.MergeTps.MainChain = mainChainTps.ToString("F2");
            overviewResp.MergeTps.SideChain = sideChainTps.ToString("F2");
            overviewResp.MergeTps.Total = (sideChainTps + mainChainTps).ToString("F2");

            overviewResp.MergeTransactions.Total =
                overviewResp.MergeTransactions.MainChain + overviewResp.MergeTransactions.SideChain;
            overviewResp.MergeAccounts.Total =
                overviewResp.MergeAccounts.MainChain + overviewResp.MergeAccounts.SideChain;
            overviewResp.MergeNfts.Total = overviewResp.MergeNfts.MainChain + overviewResp.MergeNfts.SideChain;
            overviewResp.MergeTokens.Total = overviewResp.MergeTokens.MainChain + overviewResp.MergeTokens.SideChain;
            DataStrategyLogger.LogInformation("Set home page overview success: merge chain");
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "get home page overview err,chainId:merge chain");
        }

        return overviewResp;
    }


    public override string DisplayKey(string chainId)
    {
        return RedisKeyHelper.HomeOverview(chainId);
    }
}