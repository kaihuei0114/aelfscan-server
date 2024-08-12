using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Volo.Abp.Caching;
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
    protected IDistributedCache<string> _cache { get; set; }

    protected DataStrategyBase(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<DataStrategyBase<TInput, TOutPut>> logger, IDistributedCache<string> cache) : base(optionsAccessor)
    {
        DataStrategyLogger = logger;
        _cache = cache;
    }

    public async Task LoadData(TInput input)
    {
        var queryData = await QueryData(input);
        await SaveData(queryData, input);
    }

    public abstract Task<TOutPut> QueryData(TInput input);

    public async Task SaveData(TOutPut data, TInput input)
    {
        try
        {
            var key = DisplayKey(input);
            var value = JsonConvert.SerializeObject(data);

            _cache.Set(key, value);
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
            var key = DisplayKey(input);
            var s = _cache.Get(key);
            return JsonConvert.DeserializeObject<TOutPut>(s);
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "DisplayData error");
            return default;
        }
    }

    public abstract string DisplayKey(TInput input);
}