using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Worker.Core.Provider;
using Elasticsearch.Net;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Dtos.Indexer;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Options;
using AElfScanServer.HttpApi.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Nito.AsyncEx;
using StackExchange.Redis;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.Worker.Core.Service;

public interface ITransactionService
{
    public Task UpdateTransactionRatePerMinuteAsync();

    public Task UpdateChartDataAsync();
}

public class TransactionService : AbpRedisCache, ITransactionService, ITransientDependency
{
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly BlockChainIndexerProvider _blockChainIndexerProvider;
    private readonly HomePageProvider _homePageProvider;
    private readonly IOptionsMonitor<AELFIndexerOptions> _aelfIndexerOptions;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IStorageProvider _storageProvider;
    private readonly IElasticClient _elasticClient;
    private readonly IOptionsMonitor<PullTransactionChainIdsOptions> _workerOptions;

    private readonly IEntityMappingRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly IEntityMappingRepository<AddressIndex, string> _addressIndexRepository;
    private readonly IEntityMappingRepository<LogEventIndex, string> _logEventIndexRepository;
    private readonly IEntityMappingRepository<TokenInfoIndex, string> _tokenInfoIndexRepository;
    private readonly IEntityMappingRepository<BlockExtraIndex, string> _blockExtraIndexRepository;
    private readonly ILogger<TransactionService> _logger;
    private static List<TransactionIndex> batchTransactions = new List<TransactionIndex>();
    private static long batchWriteTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static readonly object lockObject = new object();
    private static Timer timer;
    private static long PullTransactioninterval = 500 - 1;

    public TransactionService(IOptions<RedisCacheOptions> optionsAccessor, AELFIndexerProvider aelfIndexerProvider,
        IOptionsMonitor<AELFIndexerOptions> aelfIndexerOptions,
        IEntityMappingRepository<TransactionIndex, string> transactionIndexRepository,
        ILogger<TransactionService> logger, IObjectMapper objectMapper,
        IEntityMappingRepository<AddressIndex, string> addressIndexRepository,
        IEntityMappingRepository<TokenInfoIndex, string> tokenInfoIndexRepository,
        IOptionsMonitor<GlobalOptions> blockChainOptions,
        IEntityMappingRepository<BlockExtraIndex, string> blockExtraIndexRepository,
        HomePageProvider homePageProvider,
        IEntityMappingRepository<LogEventIndex, string> logEventIndexRepository, IStorageProvider storageProvider,
        IOptionsMonitor<ElasticsearchOptions> options, BlockChainIndexerProvider blockChainIndexerProvider,
        IOptionsMonitor<PullTransactionChainIdsOptions> workerOptions) :
        base(optionsAccessor)
    {
        _aelfIndexerProvider = aelfIndexerProvider;
        _aelfIndexerOptions = aelfIndexerOptions;
        _transactionIndexRepository = transactionIndexRepository;
        _logger = logger;
        _objectMapper = objectMapper;
        _addressIndexRepository = addressIndexRepository;

        _tokenInfoIndexRepository = tokenInfoIndexRepository;
        _blockExtraIndexRepository = blockExtraIndexRepository;
        _globalOptions = blockChainOptions;
        _homePageProvider = homePageProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _logEventIndexRepository = logEventIndexRepository;
        _storageProvider = storageProvider;
        var uris = options.CurrentValue.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
        _workerOptions = workerOptions;
    }

    public async Task UpdateChartDataAsync()
    {
        var chainId = "AELF";
        await ConnectAsync();
        var redisValue = RedisDatabase.StringGet(RedisKeyHelper.ChartDataLastBlockHeight(chainId));
        var lastBlockHeight = redisValue.IsNullOrEmpty ? 1 : long.Parse(redisValue) + 1;

        var batchTransactionList =
            await GetBatchTransactionList(chainId, lastBlockHeight, lastBlockHeight + PullTransactioninterval);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await HandlerDailyTransactionsAsync(batchTransactionList, chainId);
        await HandlerUniqueAddressesAsync(batchTransactionList, chainId);
        await HandlerDailyActiveAddressesAsync(batchTransactionList, chainId);
        stopwatch.Stop();
        _logger.LogInformation("Handler transaction data chainId:{0},count:{1},time:{2}", chainId,
            batchTransactionList.Count, stopwatch.Elapsed.TotalSeconds);

        RedisDatabase.StringSet(RedisKeyHelper.ChartDataLastBlockHeight(chainId),
            lastBlockHeight + PullTransactioninterval);
    }

    public async Task HandlerDailyActiveAddressesAsync(List<IndexerTransactionDto> list, string chainId)
    {
        var activeAddressesDic = new Dictionary<long, HashSet<string>>();
        var sendActiveAddressesDic = new Dictionary<long, HashSet<string>>();
        var receiveActiveAddressesDic = new Dictionary<long, HashSet<string>>();
        foreach (var indexerTransactionDto in list)
        {
            var date = DateTimeHelper.GetDateTotalMilliseconds(indexerTransactionDto.BlockTime);

            if (activeAddressesDic.TryGetValue(date, out var v))
            {
                v.Add(indexerTransactionDto.From);
                v.Add(indexerTransactionDto.To);
            }
            else
            {
                activeAddressesDic[date] = new HashSet<string> { indexerTransactionDto.From, indexerTransactionDto.To };
            }


            if (sendActiveAddressesDic.TryGetValue(date, out var sendV))
            {
                sendV.Add(indexerTransactionDto.From);
            }
            else
            {
                sendActiveAddressesDic[date] = new HashSet<string>
                    { indexerTransactionDto.From };
            }


            if (receiveActiveAddressesDic.TryGetValue(date, out var receiveV))
            {
                receiveV.Add(indexerTransactionDto.To);
            }
            else
            {
                receiveActiveAddressesDic[date] = new HashSet<string>
                    { indexerTransactionDto.To };
            }
        }

        await ConnectAsync();
        var stringGet = RedisDatabase.StringGet(RedisKeyHelper.DailyActiveAddresses(chainId));
        if (stringGet.IsNullOrEmpty)
        {
            var firstActiveAddresses = activeAddressesDic.Select(c => new DailyActiveAddressCount()
            {
                Date = c.Key,
                AddressCount = c.Value.Count,
                SendAddressCount = sendActiveAddressesDic[c.Key].Count,
                ReceiveAddressCount = receiveActiveAddressesDic[c.Key].Count,
            }).ToList().OrderBy(c => c.Date);

            var data = JsonConvert.SerializeObject(firstActiveAddresses);
            RedisDatabase.StringSet(RedisKeyHelper.DailyActiveAddresses(chainId), data);

            foreach (var keyValuePair in activeAddressesDic)
            {
                RedisDatabase.SetAdd(RedisKeyHelper.DailyActiveAddressesSet(chainId, keyValuePair.Key),
                    keyValuePair.Value.Select(c => (RedisValue)c).ToArray());
                RedisDatabase.SetAdd(RedisKeyHelper.DailySendActiveAddressesSet(chainId, keyValuePair.Key),
                    sendActiveAddressesDic[keyValuePair.Key].Select(c => (RedisValue)c).ToArray());

                RedisDatabase.SetAdd(RedisKeyHelper.DailyReceiveAddressesSet(chainId, keyValuePair.Key),
                    receiveActiveAddressesDic[keyValuePair.Key].Select(c => (RedisValue)c).ToArray());
            }

            return;
        }

        var updateActiveAddresses = JsonConvert.DeserializeObject<List<DailyActiveAddressCount>>(stringGet);

        var updateActiveAddressesDic = updateActiveAddresses.ToDictionary(p => p.Date, p => p);

        foreach (var keyValuePair in activeAddressesDic)
        {
            RedisDatabase.SetAdd(RedisKeyHelper.DailyActiveAddressesSet(chainId, keyValuePair.Key),
                keyValuePair.Value.Select(c => (RedisValue)c).ToArray());
            RedisDatabase.SetAdd(RedisKeyHelper.DailySendActiveAddressesSet(chainId, keyValuePair.Key),
                sendActiveAddressesDic[keyValuePair.Key].Select(c => (RedisValue)c).ToArray());

            RedisDatabase.SetAdd(RedisKeyHelper.DailyReceiveAddressesSet(chainId, keyValuePair.Key),
                receiveActiveAddressesDic[keyValuePair.Key].Select(c => (RedisValue)c).ToArray());

            var newActiveAddressCount =
                RedisDatabase.SetLength(RedisKeyHelper.DailyActiveAddressesSet(chainId, keyValuePair.Key));
            var newSendActiveAddressCount =
                RedisDatabase.SetLength(RedisKeyHelper.DailySendActiveAddressesSet(chainId, keyValuePair.Key));
            var newReceiveActiveAddressCount =
                RedisDatabase.SetLength(RedisKeyHelper.DailyReceiveAddressesSet(chainId, keyValuePair.Key));


            if (updateActiveAddressesDic.TryGetValue(keyValuePair.Key, out var value))
            {
                value.AddressCount = newActiveAddressCount;
                value.SendAddressCount = newSendActiveAddressCount;
                value.ReceiveAddressCount = newReceiveActiveAddressCount;
                _logger.LogInformation(
                    "Update active address count chainId:{0},date:{1},address count:{2},send address count:{3},receive address count:{4}",
                    chainId,
                    DateTimeHelper.GetDateTimeString(keyValuePair.Key), newActiveAddressCount,
                    newSendActiveAddressCount, newReceiveActiveAddressCount);
            }
            else
            {
                updateActiveAddressesDic[keyValuePair.Key] = new DailyActiveAddressCount()
                {
                    Date = keyValuePair.Key,
                    AddressCount = newActiveAddressCount,
                    SendAddressCount = newSendActiveAddressCount,
                    ReceiveAddressCount = newReceiveActiveAddressCount
                };
            }
        }

        var updateActiveAddressesList = updateActiveAddressesDic.Select(c => c.Value).ToList().OrderBy(c => c.Date);
        var serializeObject = JsonConvert.SerializeObject(updateActiveAddressesList);
        RedisDatabase.StringSet(RedisKeyHelper.DailyActiveAddresses(chainId), serializeObject);
    }

    public async Task HandlerUniqueAddressesAsync(List<IndexerTransactionDto> list, string chainId)
    {
        var uniqueAddressesDic = new Dictionary<string, long>();
        foreach (var indexerTransactionDto in list)
        {
            var date = DateTimeHelper.GetDateTotalMilliseconds(indexerTransactionDto.BlockTime);

            if (uniqueAddressesDic.TryGetValue(indexerTransactionDto.From, out var fromDate))
            {
                uniqueAddressesDic[indexerTransactionDto.From] = date < fromDate ? date : fromDate;
            }
            else
            {
                uniqueAddressesDic.Add(indexerTransactionDto.From, date);
            }

            if (uniqueAddressesDic.TryGetValue(indexerTransactionDto.To, out var toDate))
            {
                uniqueAddressesDic[indexerTransactionDto.To] = date < toDate ? date : toDate;
            }
            else
            {
                uniqueAddressesDic.Add(indexerTransactionDto.To, date);
            }
        }

        await ConnectAsync();
        var stringGet = RedisDatabase.StringGet(RedisKeyHelper.UniqueAddresses(chainId));
        if (stringGet.IsNullOrEmpty)
        {
            var dic = new Dictionary<long, int>();
            foreach (var keyValuePair in uniqueAddressesDic)
            {
                if (dic.TryGetValue(keyValuePair.Value, out var count))
                {
                    dic[keyValuePair.Value]++;
                }
                else
                {
                    dic[keyValuePair.Value] = 1;
                }
            }

            var firstUniqueAddressCounts = dic.Select(c => new UniqueAddressCount()
            {
                Date = c.Key,
                AddressCount = c.Value
            }).ToList().OrderBy(c => c.Date);

            var data = JsonConvert.SerializeObject(firstUniqueAddressCounts);
            RedisDatabase.StringSet(RedisKeyHelper.UniqueAddresses(chainId), data);
            return;
        }


        var updateUniqueAddressCounts = JsonConvert.DeserializeObject<List<UniqueAddressCount>>(stringGet);

        var updateAddressCountsDic = updateUniqueAddressCounts.ToDictionary(c => c.Date, c => c);

        var newUniqueAddressCountsDic = new Dictionary<long, int>();
        var firstTransactionAddresses = new List<string>();

        foreach (var keyValuePair in uniqueAddressesDic)
        {
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.AddressFirstTransaction(chainId, keyValuePair.Key));
            if (redisValue.IsNullOrEmpty)
            {
                if (updateAddressCountsDic.TryGetValue(keyValuePair.Value, out var v))
                {
                    v.AddressCount++;
                    _logger.LogInformation("Update unique address count date:{0},address count:{1}",
                        DateTimeHelper.GetDateTimeString(keyValuePair.Value), v.AddressCount);
                }
                else
                {
                    if (newUniqueAddressCountsDic.TryGetValue(keyValuePair.Value, out var count))
                    {
                        newUniqueAddressCountsDic[keyValuePair.Value] = count + 1;
                    }
                    else
                    {
                        newUniqueAddressCountsDic[keyValuePair.Value] = 1;
                        firstTransactionAddresses.Add(keyValuePair.Key);
                    }
                }
            }
        }

        if (!newUniqueAddressCountsDic.IsNullOrEmpty())
        {
            var updateUniqueAddressCountsList = newUniqueAddressCountsDic.Select(c => new UniqueAddressCount()
            {
                Date = c.Key,
                AddressCount = c.Value
            }).ToList().OrderBy(c => c.Date);
            updateUniqueAddressCounts.AddRange(updateUniqueAddressCountsList);
        }

        foreach (var firstTransactionAddress in firstTransactionAddresses)
        {
            RedisDatabase.StringSet(RedisKeyHelper.AddressFirstTransaction(chainId, firstTransactionAddress),
                "-");
        }

        var serializeObject = JsonConvert.SerializeObject(updateUniqueAddressCounts);
        RedisDatabase.StringSet(RedisKeyHelper.UniqueAddresses(chainId), serializeObject);
    }

    public async Task HandlerDailyTransactionsAsync(List<IndexerTransactionDto> list, string chainId)
    {
        var nowDailyTransactionCountDic = new Dictionary<long, int>();
        var nowDailyBlockCountDic = new Dictionary<long, HashSet<long>>();

        var startScore = DateTimeHelper.GetDateTotalMilliseconds(list[0].BlockTime);
        var stopScore = DateTimeHelper.GetDateTotalMilliseconds(list[0].BlockTime);
        foreach (var indexerTransactionDto in list)
        {
            var key = DateTimeHelper.GetDateTotalMilliseconds(indexerTransactionDto.BlockTime);
            startScore = key < startScore ? key : startScore;
            stopScore = key > stopScore ? key : stopScore;


            if (nowDailyTransactionCountDic.ContainsKey(key))
            {
                nowDailyTransactionCountDic[key]++;
            }
            else
            {
                nowDailyTransactionCountDic[key] = 1;
            }

            if (nowDailyBlockCountDic.ContainsKey(key))
            {
                nowDailyBlockCountDic[key].Add(indexerTransactionDto.BlockHeight);
            }
            else
            {
                nowDailyBlockCountDic[key] = new HashSet<long> { indexerTransactionDto.BlockHeight };
            }
        }


        //update daily transaction count and block count to redis zset

        var stringGet = RedisDatabase.StringGet(RedisKeyHelper.DailyTransactionCount(chainId));
        var updateDailyTransactionCounts = new List<DailyTransactionCount> { };

        if (stringGet.IsNullOrEmpty)
        {
            foreach (var keyValuePair in nowDailyTransactionCountDic)
            {
                var element = new DailyTransactionCount()
                {
                    Date = keyValuePair.Key,
                    TransactionCount = keyValuePair.Value,
                    BlockCount = nowDailyBlockCountDic[keyValuePair.Key].Count
                };

                updateDailyTransactionCounts.Add(element);
            }
        }
        else
        {
            updateDailyTransactionCounts = JsonConvert.DeserializeObject<List<DailyTransactionCount>>(stringGet);

            var updateTransactionCountsDic = updateDailyTransactionCounts.ToDictionary(p => p.Date, p => p);

            foreach (var date in nowDailyTransactionCountDic.Keys.OrderBy(v => v).ToList())
            {
                if (updateTransactionCountsDic.TryGetValue(date, out var v))
                {
                    v.TransactionCount += nowDailyTransactionCountDic[date];
                    v.BlockCount += nowDailyBlockCountDic[date].Count;
                    _logger.LogInformation(
                        "Update daily transaction count date:{0},transaction count:{1},block count:{2}",
                        DateTimeHelper.GetDateTimeString(date), v.TransactionCount, v.BlockCount);
                }
                else
                {
                    updateDailyTransactionCounts.Add(new DailyTransactionCount()
                    {
                        Date = date,
                        TransactionCount = nowDailyTransactionCountDic[date],
                        BlockCount = nowDailyBlockCountDic[date].Count
                    });
                    _logger.LogInformation("Add daily transaction count date:{0},transaction count:{1},block count:{2}",
                        DateTimeHelper.GetDateTimeString(date), nowDailyTransactionCountDic[date],
                        nowDailyBlockCountDic[date].Count);
                }
            }
        }

        var serializeObject = JsonConvert.SerializeObject(updateDailyTransactionCounts);

        RedisDatabase.StringSet(RedisKeyHelper.DailyTransactionCount(chainId), serializeObject);
    }


    // public async Task<List<IndexerTransactionDto>> GetBatchTransactionList(string chainId, long startBlockHeight,
    //     long endBlockHeight)
    // {
    //     object _lock = new object();
    //     var batchList = new List<IndexerTransactionDto>();
    //
    //     Stopwatch stopwatch = Stopwatch.StartNew();
    //
    //     var tasks = new List<Task>();
    //     for (long i = startBlockHeight; i <= endBlockHeight; i += 100)
    //     {
    //         var start = i;
    //         var end = start + 99 > endBlockHeight ? endBlockHeight : start + 99;
    //         var findTsk = _aelfIndexerProvider.GetTransactionsAsync(chainId, start, end, "")
    //             .ContinueWith(task =>
    //             {
    //                 lock (_lock)
    //                 {
    //                     if (task.Result.IsNullOrEmpty())
    //                     {
    //                         _logger.LogError("Get batch transaction list is null,chainId:{0},start:{1},end:{2}",
    //                             chainId, start, end);
    //                         return;
    //                     }
    //
    //                     batchList.AddRange(task.Result);
    //                 }
    //             });
    //         tasks.Add(findTsk);
    //     }
    //
    //     await tasks.WhenAll();
    //
    //     stopwatch.Stop();
    //     _logger.LogInformation("Get batch transaction list from chainId:{0},start:{1},end:{2},count:{3},time:{4}",
    //         chainId, startBlockHeight, endBlockHeight, batchList.Count, stopwatch.Elapsed.TotalSeconds);
    //     return batchList;
    // }

    public async Task<List<IndexerTransactionDto>> GetBatchTransactionList(string chainId, long startBlockHeight,
        long endBlockHeight)
    {
        var batchList = new List<IndexerTransactionDto>();

        Stopwatch stopwatch = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (long i = startBlockHeight; i <= endBlockHeight; i += 100)
        {
            var start = i;
            var end = start + 99 > endBlockHeight ? endBlockHeight : start + 99;
            var data = await _aelfIndexerProvider.GetTransactionsAsync(chainId, start, end, "");
            if (data.IsNullOrEmpty())
            {
                _logger.LogError("Get batch transaction list is null,chainId:{0},start:{1},end:{2}",
                    chainId, start, end);
                continue;
            }

            batchList.AddRange(data);
        }

        await tasks.WhenAll();

        stopwatch.Stop();
        _logger.LogInformation("Get batch transaction list from chainId:{0},start:{1},end:{2},count:{3},time:{4}",
            chainId, startBlockHeight, endBlockHeight, batchList.Count, stopwatch.Elapsed.TotalSeconds);
        return batchList;
    }

    public async Task UpdateTransactionRatePerMinuteAsync()
    {
        await ConnectAsync();

        var chainIds = new List<string>();
        var mergeList = new List<List<TransactionCountPerMinuteDto>>();
        try
        {
            chainIds = _workerOptions.CurrentValue.ChainIds;


            foreach (var chainId in chainIds)
            {
                _logger.LogInformation("start find transaction info:{c}", chainId);
                var chartDataKey = RedisKeyHelper.TransactionChartData(chainId);

                var nowMilliSeconds = DateTimeHelper.GetNowMilliSeconds();
                var beforeHoursMilliSeconds = DateTimeHelper.GetBeforeHoursMilliSeconds(3);

                var transactionsAsync =
                    await _blockChainIndexerProvider.GetTransactionsAsync(chainId, 0, 1000, beforeHoursMilliSeconds,
                        nowMilliSeconds);
                if (transactionsAsync == null || transactionsAsync.Items.Count <= 0)
                {
                    transactionsAsync =
                        await _blockChainIndexerProvider.GetTransactionsAsync(chainId, 0, 1000);
                }

                if (transactionsAsync == null)
                {
                    _logger.LogError("Not query transaction list from blockchain app plugin,chainId:{e}", chainId);
                    continue;
                }

                if (transactionsAsync.Items.IsNullOrEmpty())
                {
                    _logger.LogWarning("transaction is null,chainId:{0}", chainId);
                    continue;
                }

                _logger.LogInformation("find transaction data chainId:{0},count:{1}", chainId,
                    transactionsAsync.Items.Count);

                var transactionChartData =
                    await ParseToTransactionChartDataAsync(chartDataKey, transactionsAsync.Items);


                if (transactionChartData.IsNullOrEmpty())
                {
                    _logger.LogInformation("merge transaction data is null:{0}", chainId);
                    continue;
                }

                _logger.LogInformation("transaction chart data:{c},count:{1}", chainId, transactionChartData.Count);

                if (transactionChartData.Count > 180)
                {
                    transactionChartData = transactionChartData.Skip(transactionChartData.Count - 180).ToList();
                }

                _logger.LogInformation("sub transaction chart data:{c},count:{1}", chainId, transactionChartData.Count);

                mergeList.Add(transactionChartData);
                var serializeObject = JsonConvert.SerializeObject(transactionChartData);


                await RedisDatabase.StringSetAsync(chartDataKey, serializeObject);
                _logger.LogInformation("Set transaction count per minute to cache success!!,redis key:{k}",
                    chartDataKey);
            }


            var merge = mergeList.SelectMany(c => c).GroupBy(c => c.Start).Select(c =>
                new TransactionCountPerMinuteDto()
                {
                    Start = c.Key,
                    End = c.Key + 60000,
                    Count = c.Sum(d => d.Count)
                }).ToList();

            if (merge.Count > 180)
            {
                merge = merge.Skip(merge.Count - 180).ToList();
            }

            _logger.LogInformation("merge count {c}", merge.Count);

            var mergeSerializeObject = JsonConvert.SerializeObject(merge);
            var mergeKey = RedisKeyHelper.TransactionChartData("merge");
            await RedisDatabase.StringSetAsync(RedisKeyHelper.TransactionChartData("merge"), mergeSerializeObject);

            _logger.LogInformation("Set transaction count per minute to cache success!!,redis key:{k}",
                mergeKey);
        }
        catch (Exception e)
        {
            _logger.LogError("Update transaction count per minute error:{e}", e.Message);
        }
    }


    public async Task<List<TransactionCountPerMinuteDto>> ParseToTransactionChartDataAsync(
        string key, List<IndexerTransactionInfoDto> list)
    {
        var dictionary = new Dictionary<long, TransactionCountPerMinuteDto>();
        foreach (var indexerTransactionDto in list)
        {
            var t = indexerTransactionDto.Metadata.Block.BlockTime;
            var timestamp =
                DateTimeHelper.GetTotalMilliseconds(new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, 0));


            if (dictionary.ContainsKey(timestamp))
            {
                dictionary[timestamp].Count++;
            }
            else
            {
                dictionary.Add(timestamp,
                    new TransactionCountPerMinuteDto() { Start = timestamp, End = timestamp + 60000, Count = 1 });
            }
        }

        var newList = dictionary.Values.OrderBy(c => c.Start).ToList();
        try
        {
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(key);
            if (redisValue.IsNullOrEmpty)
            {
                return newList;
            }

            var oldList = JsonConvert.DeserializeObject<List<TransactionCountPerMinuteDto>>(redisValue);

            var last = oldList.Last();
            var subOldList = oldList.GetRange(0, oldList.Count - 1);

            var subNewList = newList.Where(c => c.Start >= last.Start).ToList();

            if (!subNewList.IsNullOrEmpty() && subNewList.Count > 0)
            {
                subOldList.AddRange(subNewList);
            }

            if (subOldList.Count == 0)
            {
                subOldList = newList;
            }

            _logger.LogInformation("Merge transaction per minute data chainId:{0},oldList:{1},newList:{2}", key,
                oldList.Count, newList.Count);

            return subOldList;
        }
        catch (Exception e)
        {
            _logger.LogInformation("Parse key:{0} data to transaction err:{1}", key, e.Message);
        }

        return newList;
    }
}