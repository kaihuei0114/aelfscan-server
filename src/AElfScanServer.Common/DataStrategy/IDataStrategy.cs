using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;

namespace AElfScanServer.DataStrategy;

public interface IDataStrategy<TInput, TOutPut>
{
    Task LoadData(TInput input);
    Task<TOutPut> DisplayData(TInput input);
}

public class DataStrategyContext<TInput, TOutPut>
{
    private IDataStrategy<TInput, TOutPut> _dataStrategy;

    public DataStrategyContext(IDataStrategy<TInput, TOutPut> dataStrategy)
    {
        _dataStrategy = dataStrategy;
    }

    public async Task LoadData(TInput input)
    {
        await _dataStrategy.LoadData(input);
    }

    public async Task<TOutPut> DisplayData(TInput input)
    {
        return await _dataStrategy.DisplayData(input);
    }
}

public abstract class DataStrategyBase<TInput, TOutPut> : AbpRedisCache, IDataStrategy<TInput, TOutPut>
{
    protected ILogger<DataStrategyBase<TInput, TOutPut>> DataStrategyLogger { get; set; }

    protected DataStrategyBase(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<DataStrategyBase<TInput, TOutPut>> logger) : base(optionsAccessor)
    {
        DataStrategyLogger = logger;
    }

    public async Task LoadData(TInput input)
    {
        var queryData = await QueryData(input);
        await SaveData(queryData, input);
    }

    public virtual Task<TOutPut> QueryData(TInput input)
    {
        return default;
    }

    public async Task SaveData(TOutPut data, TInput input)
    {
        try
        {
            await ConnectAsync();
            var key = DisplayKey(input);
            var value = JsonConvert.SerializeObject(data);
            await RedisDatabase.StringSetAsync(key, value);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "SaveData error");
        }
    }


    public async Task<TOutPut> DisplayData(TInput input)
    {
        try
        {
            await ConnectAsync();
            var key = DisplayKey(input);
            var redisValue = RedisDatabase.StringGet(key);


            return JsonConvert.DeserializeObject<TOutPut>(redisValue);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "DisplayData error");
            return default;
        }
    }

    public abstract string DisplayKey(TInput input);
}