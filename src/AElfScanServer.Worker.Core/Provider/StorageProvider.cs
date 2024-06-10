using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Worker.Core.Provider;

public interface IStorageProvider
{
    public Task SetAsync<T>(string key, T data) where T : class;
    public Task<T> GetAsync<T>(string key) where T : class, new();
}

public class StorageProvider : AbpRedisCache, IStorageProvider, ITransientDependency
{
    private readonly ILogger<StorageProvider> _logger;
    private readonly IDistributedCacheSerializer _serializer;

    public StorageProvider(IOptions<RedisCacheOptions> optionsAccessor, ILogger<StorageProvider> logger,
        IDistributedCacheSerializer serializer) : base(optionsAccessor)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public async Task SetAsync<T>(string key, T data) where T : class
    {
        try
        {
            await ConnectAsync();

            await RedisDatabase.StringSetAsync(key, _serializer.Serialize(data));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Set {key} to redis error.", key);
        }
    }

    public async Task<T> GetAsync<T>(string key) where T : class, new()
    {
        try
        {
            await ConnectAsync();

            var redisValue = await RedisDatabase.StringGetAsync(key);

            _logger.LogDebug("[StorageProvider] {key} spec: {spec}", key, redisValue);

            return redisValue.HasValue ? _serializer.Deserialize<T>(redisValue) : null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get {key} error.", key);
            return null;
        }
    }
}