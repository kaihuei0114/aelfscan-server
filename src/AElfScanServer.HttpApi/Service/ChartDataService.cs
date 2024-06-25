using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.HttpApi.Dtos.ChartData;
using AElfScanServer.HttpApi.Helper;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.HttpApi.Service;

public interface IChartDataService
{
    public Task<DailyTransactionCountResp> GetDailyTransactionCountAsync(ChartDataRequest request);


    public Task<UniqueAddressCountResp> GetUniqueAddressCountAsync(ChartDataRequest request);


    public Task<ActiveAddressCountResp> GetActiveAddressCountAsync(ChartDataRequest request);
}

public class ChartDataService : AbpRedisCache, IChartDataService, ITransientDependency
{
    private readonly ILogger<ChartDataService> _logger;

    public ChartDataService(IOptions<RedisCacheOptions> optionsAccessor, ILogger<ChartDataService> logger) : base(
        optionsAccessor)
    {
        _logger = logger;
    }

    public async Task<DailyTransactionCountResp> GetDailyTransactionCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var dailyTransactionCountResp = new DailyTransactionCountResp()
        {
            ChainId = request.ChainId,
            List = new List<DailyTransactionCount>()
        };
        var key = RedisKeyHelper.DailyTransactionCount(request.ChainId);
        var value = RedisDatabase.StringGet(key);

        dailyTransactionCountResp.List
            = JsonConvert.DeserializeObject<List<DailyTransactionCount>>(value);

        dailyTransactionCountResp.HighestTransactionCount =
            dailyTransactionCountResp.List.MaxBy(c => c.TransactionCount);

        dailyTransactionCountResp.LowesTransactionCount =
            dailyTransactionCountResp.List.MinBy(c => c.TransactionCount);

        return dailyTransactionCountResp;
    }

    public async Task<UniqueAddressCountResp> GetUniqueAddressCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var uniqueAddressCountResp = new UniqueAddressCountResp()
        {
            ChainId = request.ChainId,
            List = new List<UniqueAddressCount>()
        };
        var key = RedisKeyHelper.UniqueAddresses(request.ChainId);
        var value = RedisDatabase.StringGet(key);

        uniqueAddressCountResp.List
            = JsonConvert.DeserializeObject<List<UniqueAddressCount>>(value);

        uniqueAddressCountResp.HighestIncrease =
            uniqueAddressCountResp.List.MaxBy(c => c.AddressCount);

        uniqueAddressCountResp.LowestIncrease =
            uniqueAddressCountResp.List.MinBy(c => c.AddressCount);

        return uniqueAddressCountResp;
    }

    public async Task<ActiveAddressCountResp> GetActiveAddressCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var activeAddressCountResp = new ActiveAddressCountResp()
        {
            ChainId = request.ChainId,
            List = new List<DailyActiveAddressCount>()
        };
        var value = RedisDatabase.StringGet(RedisKeyHelper.DailyActiveAddresses(request.ChainId));

        activeAddressCountResp.List
            = JsonConvert.DeserializeObject<List<DailyActiveAddressCount>>(value);

        activeAddressCountResp.HighestActiveCount =
            activeAddressCountResp.List.MaxBy(c => c.AddressCount);

        activeAddressCountResp.LowestActiveCount =
            activeAddressCountResp.List.MinBy(c => c.AddressCount);

        return activeAddressCountResp;
    }
}