using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.DataStrategy;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;

namespace AElfScanServer.BlockChain.HttpApi.DataStrategy;

public class OverviewDataStrategy : AbpRedisCache, IDataStrategy<string, HomeOverviewResponseDto>
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly HomePageProvider _homePageProvider;
    private readonly BlockChainDataProvider _blockChainProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;

    private readonly ILogger<OverviewDataStrategy> _logger;


    public OverviewDataStrategy(IOptions<RedisCacheOptions> optionsAccessor,
        IOptionsMonitor<GlobalOptions> globalOptions,
        AELFIndexerProvider aelfIndexerProvider,
        HomePageProvider homePageProvider,
        BlockChainDataProvider blockChainProvider,
        ITokenIndexerProvider tokenIndexerProvider,
        IBlockChainIndexerProvider blockChainIndexerProvider,
        ILogger<OverviewDataStrategy> logger) : base(optionsAccessor)
    {
        _globalOptions = globalOptions;
        _aelfIndexerProvider = aelfIndexerProvider;
        _homePageProvider = homePageProvider;
        _blockChainProvider = blockChainProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _logger = logger;
    }

    public async Task LoadData(string chainId)
    {
        _logger.LogInformation("GetBlockchainOverviewAsync:{c}", chainId);
        var overviewResp = new HomeOverviewResponseDto();
        if (!_globalOptions.CurrentValue.ChainIds.Exists(s => s == chainId))
        {
            _logger.LogWarning("Get blockchain overview chainId not exist:{c},chainIds:{l}", chainId,
                _globalOptions.CurrentValue.ChainIds);
            return;
        }

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
            tasks.Add(_homePageProvider.GetTransactionCount(chainId).ContinueWith(
                task => { overviewResp.Tps = task.Result; }));

            await Task.WhenAll(tasks);

            var serializeObject = JsonConvert.SerializeObject(overviewResp);

            await ConnectAsync();

            var homeOverview = RedisKeyHelper.HomeOverview(chainId);
            RedisDatabase.StringSet(homeOverview, serializeObject);
            _logger.LogInformation("Set home page overview success:{c}", chainId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get home page overview err,chainId:{c}", chainId);
        }
    }

    public async Task<HomeOverviewResponseDto> DisplayData(string chainId)
    {
        var homeOverviewResponseDto = new HomeOverviewResponseDto();
        try
        {
            await ConnectAsync();
            var key = RedisKeyHelper.HomeOverview(chainId);
            var redisValue = RedisDatabase.StringGet(key);
            if (!redisValue.HasValue)
            {
                _logger.LogError("Get home page overview is null:{c}", chainId);
                return homeOverviewResponseDto;
            }

            return JsonConvert.DeserializeObject<HomeOverviewResponseDto>(redisValue);
        }
        catch (Exception e)
        {
            _logger.LogError("Get home page overview error:{e}", e.Message);
        }

        return homeOverviewResponseDto;
    }
}