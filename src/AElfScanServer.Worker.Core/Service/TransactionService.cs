using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Contracts.Consensus.AEDPoS;
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
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Nito.AsyncEx;
using StackExchange.Redis;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using Math = System.Math;
using Timer = System.Timers.Timer;

namespace AElfScanServer.Worker.Core.Service;

public interface ITransactionService
{
    public Task UpdateTransactionRatePerMinuteAsync();

    public Task UpdateChartDataAsync();


    public Task UpdateNetwork();


    public Task UpdateDailyNetwork();
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

    private readonly IEntityMappingRepository<RoundIndex, string> _roundIndexRepository;
    private readonly IEntityMappingRepository<NodeBlockProduceIndex, string> _nodeBlockProduceRepository;
    private readonly IEntityMappingRepository<DailyBlockProduceCountIndex, string> _blockProduceRepository;
    private readonly IEntityMappingRepository<DailyBlockProduceDurationIndex, string> _blockProduceDurationRepository;
    private readonly IEntityMappingRepository<DailyCycleCountIndex, string> _cycleCountRepository;
    private readonly ILogger<TransactionService> _logger;
    private static bool FinishInitChartData = false;

    private static Timer timer;
    private static long PullTransactioninterval = 1000 - 1;

    public TransactionService(IOptions<RedisCacheOptions> optionsAccessor, AELFIndexerProvider aelfIndexerProvider,
        IOptionsMonitor<AELFIndexerOptions> aelfIndexerOptions,
        ILogger<TransactionService> logger, IObjectMapper objectMapper,
        IOptionsMonitor<GlobalOptions> blockChainOptions,
        HomePageProvider homePageProvider, IStorageProvider storageProvider,
        IOptionsMonitor<ElasticsearchOptions> options, BlockChainIndexerProvider blockChainIndexerProvider,
        IOptionsMonitor<PullTransactionChainIdsOptions> workerOptions,
        IEntityMappingRepository<RoundIndex, string> roundIndexRepository,
        IEntityMappingRepository<NodeBlockProduceIndex, string> nodeBlockProduceRepository,
        IEntityMappingRepository<DailyBlockProduceCountIndex, string> blockProduceRepository,
        IEntityMappingRepository<DailyBlockProduceDurationIndex, string> blockProduceDurationRepository,
        IEntityMappingRepository<DailyCycleCountIndex, string> cycleCountRepository) :
        base(optionsAccessor)
    {
        _aelfIndexerProvider = aelfIndexerProvider;
        _aelfIndexerOptions = aelfIndexerOptions;
        _logger = logger;
        _objectMapper = objectMapper;

        _globalOptions = blockChainOptions;
        _homePageProvider = homePageProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _storageProvider = storageProvider;
        var uris = options.CurrentValue.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
        _workerOptions = workerOptions;
        _roundIndexRepository = roundIndexRepository;
        _nodeBlockProduceRepository = nodeBlockProduceRepository;
        _blockProduceRepository = blockProduceRepository;
        _blockProduceDurationRepository = blockProduceDurationRepository;
        _cycleCountRepository = cycleCountRepository;
    }


    public async Task UpdateDailyNetwork()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            var queryable = await _roundIndexRepository.GetQueryableAsync();
            var todayTotalSeconds = DateTimeHelper.GetTodayTotalSeconds();
            var tomorrowTotalSeconds = DateTimeHelper.GetTomorrowTotalSeconds();

            var list = queryable.Where(w => w.StartTime >= todayTotalSeconds)
                .Where(w => w.StartTime < tomorrowTotalSeconds).Where(c => c.ChainId == chainId).ToList();

            if (list.IsNullOrEmpty())
            {
                return;
            }

            var blockProduceIndex = new DailyBlockProduceCountIndex()
            {
                Date = todayTotalSeconds * 1000,
                ChainId = chainId
            };

            var dailyCycleCountIndex = new DailyCycleCountIndex()
            {
                Date = todayTotalSeconds * 1000,
                ChainId = chainId
            };

            var dailyBlockProduceDurationIndex = new DailyBlockProduceDurationIndex()
            {
                Date = todayTotalSeconds * 1000,
                ChainId = chainId
            };


            var totalDuration = 0l;
            decimal longestBlockDuration = 0;
            decimal shortestBlockDuration = 0;
            foreach (var round in list)
            {
                blockProduceIndex.BlockCount += round.Blcoks;
                blockProduceIndex.MissedBlockCount += round.MissedBlocks;

                dailyCycleCountIndex.CycleCount++;
                totalDuration += round.DurationSeconds;
                if (round.Blcoks == 0)
                {
                    dailyCycleCountIndex.MissedCycle++;
                }

                if (round.Blcoks == 0 || round.DurationSeconds == 0)
                {
                    _logger.LogWarning("Round duration or blocks is zero,chainId:{0},round number:{1}", chainId,
                        round.RoundNumber);
                    continue;
                }

                var roundDurationSeconds = round.DurationSeconds / (decimal)round.Blcoks;

                if (longestBlockDuration == 0)
                {
                    longestBlockDuration = roundDurationSeconds;
                }
                else
                {
                    longestBlockDuration =
                        Math.Max(longestBlockDuration, roundDurationSeconds);
                }


                if (shortestBlockDuration == 0)
                {
                    shortestBlockDuration = roundDurationSeconds;
                }
                else
                {
                    shortestBlockDuration =
                        Math.Min(shortestBlockDuration, roundDurationSeconds);
                }
            }

            dailyCycleCountIndex.MissedBlockCount = blockProduceIndex.MissedBlockCount;
            dailyBlockProduceDurationIndex.AvgBlockDuration =
                (totalDuration / (decimal)blockProduceIndex.BlockCount).ToString("F2");
            dailyBlockProduceDurationIndex.LongestBlockDuration = longestBlockDuration.ToString("F2");
            dailyBlockProduceDurationIndex.ShortestBlockDuration = shortestBlockDuration.ToString("F2");

            decimal result = blockProduceIndex.BlockCount /
                             (decimal)(blockProduceIndex.BlockCount + blockProduceIndex.MissedBlockCount);
            blockProduceIndex.BlockProductionRate = result.ToString("F2");

            await _blockProduceRepository.AddOrUpdateAsync(blockProduceIndex);
            await _blockProduceDurationRepository.AddOrUpdateAsync(dailyBlockProduceDurationIndex);
            await _cycleCountRepository.AddOrUpdateAsync(dailyCycleCountIndex);
            _logger.LogInformation("Insert daily network statistic count index chainId:{0},date:{1}", chainId,
                DateTimeHelper.GetDateTimeString(todayTotalSeconds * 1000));
        }
    }

    public async Task UpdateNetworkTmp()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            var client = new AElfClient(_globalOptions.CurrentValue.ChainNodeHosts[chainId]);
            var empty = new Empty();
            var currentValueContractAddressConsensu = _globalOptions.CurrentValue.ContractAddressConsensus[chainId];
            if (currentValueContractAddressConsensu.IsNullOrEmpty())
            {
                return;
            }

            var transaction = await client.GenerateTransactionAsync(
                client.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                currentValueContractAddressConsensu,
                "GetCurrentRoundInformation", empty);

            var signTransaction = client.SignTransaction(GlobalOptions.PrivateKey, transaction);

            var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto()
            {
                RawTransaction = HexByteConvertorExtensions.ToHex(signTransaction.ToByteArray())
            });


            var round = Round.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(result));

            var findRoundNumber = 0l;
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.LatestRound(chainId));
            if (redisValue.IsNullOrEmpty)
            {
                findRoundNumber = round.RoundNumber - 1;
            }
            else
            {
                findRoundNumber = long.Parse(redisValue) + 1;
                if (findRoundNumber >= round.RoundNumber)
                {
                    findRoundNumber = round.RoundNumber - 1;
                }
            }


            var int64Value = new Int64Value()
            {
                Value = findRoundNumber
            };

            transaction = await client.GenerateTransactionAsync(
                client.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                _globalOptions.CurrentValue.ContractAddressConsensus[chainId],
                "GetRoundInformation", int64Value);

            signTransaction = client.SignTransaction(GlobalOptions.PrivateKey, transaction);

            result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto()
            {
                RawTransaction = HexByteConvertorExtensions.ToHex(signTransaction.ToByteArray())
            });

            round = Round.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(result));

            await StatisticRoundInfo(round, chainId);

            RedisDatabase.StringSet(RedisKeyHelper.LatestRound(chainId), findRoundNumber);
        }
    }


    public async Task UpdateNetwork()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            var findRoundNumber = 0l;
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Start();
                var client = new AElfClient(_globalOptions.CurrentValue.ChainNodeHosts[chainId]);
                var currentValueContractAddressConsensu = _globalOptions.CurrentValue.ContractAddressConsensus[chainId];
                if (currentValueContractAddressConsensu.IsNullOrEmpty())
                {
                    return;
                }


                await ConnectAsync();
                var redisValue = RedisDatabase.StringGet(RedisKeyHelper.LatestRound(chainId));
                if (redisValue.IsNullOrEmpty)
                {
                    return;
                }

                findRoundNumber = (long)redisValue;

                var int64Value = new Int64Value()
                {
                    Value = (long)redisValue
                };

                _logger.LogInformation("UpdateNetwork round:{r} start:{c}", findRoundNumber, chainId);
                var transaction = await client.GenerateTransactionAsync(
                    client.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    _globalOptions.CurrentValue.ContractAddressConsensus[chainId],
                    "GetRoundInformation", int64Value);

                var signTransaction = client.SignTransaction(GlobalOptions.PrivateKey, transaction);

                var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto()
                {
                    RawTransaction = HexByteConvertorExtensions.ToHex(signTransaction.ToByteArray())
                });

                var round = Round.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(result));

                await StatisticRoundInfo(round, chainId);

                RedisDatabase.StringSet(RedisKeyHelper.LatestRound(chainId), findRoundNumber + 1);
                stopwatch.Stop();
                _logger.LogInformation("Cost time Update network statistic chainId:{0},time:{1}", chainId,
                    stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception e)
            {
                _logger.LogError("Update network Err round:{r},chainId:{c},error:{e}", findRoundNumber, chainId,
                    e.Message);
                Thread.Sleep(1000 * 60);
            }
        }
    }


    internal async Task StatisticRoundInfo(Round round, string chainId)
    {
        var roundIndex = new RoundIndex()
        {
            ChainId = chainId,
            RoundNumber = round.RoundNumber,
            ProduceBlockBpAddresses = new List<string>(),
            NotProduceBlockBpAddresses = new List<string>()
        };

        var batch = new List<NodeBlockProduceIndex>();
        var client = new AElfClient(_globalOptions.CurrentValue.ChainNodeHosts[chainId]);
        foreach (var minerInRound in round.RealTimeMinersInformation)
        {
            var bpAddress = client.GetAddressFromPubKey(minerInRound.Key);
            var nodeInfo = new NodeBlockProduceIndex()
            {
                ChainId = chainId,
                RoundNumber = round.RoundNumber,
                NodeAddress = bpAddress
            };

            roundIndex.Blcoks += minerInRound.Value.ActualMiningTimes.Count;
            nodeInfo.Blcoks = minerInRound.Value.ActualMiningTimes.Count;

            var expectBlocks = minerInRound.Value.ActualMiningTimes.Count > 8 ? 15 : 8;

            roundIndex.MissedBlocks += expectBlocks - minerInRound.Value.ActualMiningTimes.Count;
            nodeInfo.MissedBlocks = expectBlocks - minerInRound.Value.ActualMiningTimes.Count;
            nodeInfo.IsExtraBlockProducer = minerInRound.Value.IsExtraBlockProducer;


            if (minerInRound.Value.ActualMiningTimes.IsNullOrEmpty())
            {
                roundIndex.NotProduceBlockBpCount++;
                roundIndex.NotProduceBlockBpAddresses.Add(bpAddress);
            }
            else
            {
                roundIndex.ProduceBlockBpCount++;
                roundIndex.ProduceBlockBpAddresses.Add(bpAddress);

                var min = minerInRound.Value.ActualMiningTimes.Select(c => c.Seconds).Min() * 1000;
                var max = minerInRound.Value.ActualMiningTimes.Select(c => c.Seconds).Max() * 1000;

                nodeInfo.StartTime = min;
                nodeInfo.EndTime = max;
                if (roundIndex.StartTime == 0)
                {
                    roundIndex.StartTime = min;
                }
                else
                {
                    roundIndex.StartTime = Math.Min(roundIndex.StartTime, min);
                }

                roundIndex.EndTime = Math.Max(roundIndex.EndTime, max);
            }

            nodeInfo.DurationSeconds = nodeInfo.EndTime - nodeInfo.StartTime;


            batch.Add(nodeInfo);
        }

        roundIndex.DurationSeconds = roundIndex.EndTime - roundIndex.StartTime;


        await _roundIndexRepository.AddOrUpdateAsync(roundIndex);
        _logger.LogInformation("Insert round index chainId:{0},round number:{1},date:{2}", chainId, round.RoundNumber,
            DateTimeHelper.GetDateTimeString(roundIndex.StartTime));
        await _nodeBlockProduceRepository.AddOrUpdateManyAsync(batch);
        _logger.LogInformation("Insert node block produce index chainId:{0},round number:{1},date:{2}", chainId,
            round.RoundNumber, DateTimeHelper.GetDateTimeString(roundIndex.StartTime));
    }

    public async Task UpdateChartDataAsync()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.ChartDataLastBlockHeight(chainId));
            var lastBlockHeight = redisValue.IsNullOrEmpty ? 1 : long.Parse(redisValue) + 1;
            var batchTransactionList =
                await GetBatchTransactionList(chainId, lastBlockHeight, lastBlockHeight + PullTransactioninterval);


            if (batchTransactionList.IsNullOrEmpty())
            {
                _logger.LogInformation("batchTransactionList is null: start:{0},end:{1}", lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);
            }

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
    }

    public async Task HandlerDailyActiveAddressesAsync(List<IndexerTransactionDto> list, string chainId)
    {
        var activeAddressesDic = new Dictionary<long, HashSet<string>>();
        var sendActiveAddressesDic = new Dictionary<long, HashSet<string>>();
        var receiveActiveAddressesDic = new Dictionary<long, HashSet<string>>();
        foreach (var indexerTransactionDto in list)
        {
            var date = DateTimeHelper.GetDateTotalMilliseconds(indexerTransactionDto.BlockTime);

            if (date == 0 && indexerTransactionDto.BlockHeight == 1)
            {
                date = _globalOptions.CurrentValue.OneBlockTime[chainId];
            }

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
            if (date == 0 && indexerTransactionDto.BlockHeight == 1)
            {
                date = _globalOptions.CurrentValue.OneBlockTime[chainId];
            }


            if (uniqueAddressesDic.TryGetValue(indexerTransactionDto.From, out var fromDate))
            {
                uniqueAddressesDic[indexerTransactionDto.From] = fromDate == 0 ? date : Math.Min(date, fromDate);
            }
            else
            {
                uniqueAddressesDic.Add(indexerTransactionDto.From, date);
            }

            if (uniqueAddressesDic.TryGetValue(indexerTransactionDto.To, out var toDate))
            {
                uniqueAddressesDic[indexerTransactionDto.To] = toDate == 0 ? toDate : Math.Min(date, toDate);
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


            var insertData = JsonConvert.SerializeObject(firstUniqueAddressCounts);

            RedisDatabase.StringSet(RedisKeyHelper.UniqueAddresses(chainId), insertData);
            foreach (var keyPair in uniqueAddressesDic)
            {
                RedisDatabase.SetAdd(RedisKeyHelper.UniqueAddressesHashSet(chainId), keyPair.Key);
            }

            return;
        }


        var updateUniqueAddressCounts = JsonConvert.DeserializeObject<List<UniqueAddressCount>>(stringGet);

        var updateAddressCountsDic = updateUniqueAddressCounts.ToDictionary(c => c.Date, c => c);


        foreach (var keyValuePair in uniqueAddressesDic)
        {
            await ConnectAsync();

            if (RedisDatabase.SetContains(RedisKeyHelper.UniqueAddressesHashSet(chainId), keyValuePair.Key))
            {
                continue;
            }


            if (updateAddressCountsDic.TryGetValue(keyValuePair.Value, out var v))
            {
                v.AddressCount++;
                _logger.LogInformation("Update unique address count date:{0},address count:{1}",
                    DateTimeHelper.GetDateTimeString(keyValuePair.Value), v.AddressCount);
            }
            else
            {
                updateAddressCountsDic[keyValuePair.Value] = new UniqueAddressCount()
                {
                    Date = keyValuePair.Value,
                    AddressCount = 1
                };
            }

            RedisDatabase.SetAdd(RedisKeyHelper.UniqueAddressesHashSet(chainId), keyValuePair.Key);
        }


        updateUniqueAddressCounts = updateAddressCountsDic.Values.Select(c => c).OrderBy(c => c.Date).ToList();


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

            if (key == 0 && indexerTransactionDto.BlockHeight == 1)
            {
                key = _globalOptions.CurrentValue.OneBlockTime[chainId];
            }

            startScore = Math.Min(key, startScore);
            stopScore = Math.Max(key, stopScore);


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


            var d = JsonConvert.SerializeObject(updateDailyTransactionCounts);

            RedisDatabase.StringSet(RedisKeyHelper.DailyTransactionCount(chainId), d);
            return;
        }

        updateDailyTransactionCounts = JsonConvert.DeserializeObject<List<DailyTransactionCount>>(stringGet);

        var updateTransactionCountsDic = updateDailyTransactionCounts.ToDictionary(p => p.Date, p => p);


        foreach (var keyValuePair in nowDailyTransactionCountDic)
        {
            var date = keyValuePair.Key;
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
                updateTransactionCountsDic[date] = new DailyTransactionCount()
                {
                    TransactionCount = nowDailyTransactionCountDic[date],
                    BlockCount = nowDailyBlockCountDic[date].Count
                };
                _logger.LogInformation("Add daily transaction count date:{0},transaction count:{1},block count:{2}",
                    DateTimeHelper.GetDateTimeString(keyValuePair.Key), nowDailyTransactionCountDic[date],
                    nowDailyBlockCountDic[date].Count);
            }
        }


        var serializeObject = JsonConvert.SerializeObject(updateDailyTransactionCounts);

        RedisDatabase.StringSet(RedisKeyHelper.DailyTransactionCount(chainId), serializeObject);
    }


    public async Task<List<IndexerTransactionDto>> GetBatchTransactionList(string chainId, long startBlockHeight,
        long endBlockHeight)
    {
        object _lock = new object();
        var batchList = new List<IndexerTransactionDto>();

        Stopwatch stopwatch = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (long i = startBlockHeight; i <= endBlockHeight; i += 100)
        {
            var start = i;
            var end = start + 99 > endBlockHeight ? endBlockHeight : start + 99;
            var findTsk = _aelfIndexerProvider.GetTransactionsAsync(chainId, start, end, "")
                .ContinueWith(task =>
                {
                    lock (_lock)
                    {
                        if (task.Result.IsNullOrEmpty())
                        {
                            _logger.LogError("Get batch transaction list is null,chainId:{0},start:{1},end:{2}",
                                chainId, start, end);
                            return;
                        }

                        batchList.AddRange(task.Result);
                    }
                });
            tasks.Add(findTsk);
        }

        await tasks.WhenAll();

        stopwatch.Stop();
        _logger.LogInformation("Get batch transaction list from chainId:{0},start:{1},end:{2},count:{3},time:{4}",
            chainId, startBlockHeight, endBlockHeight, batchList.Count, stopwatch.Elapsed.TotalSeconds);
        return batchList;
    }

    // public async Task<List<IndexerTransactionDto>> GetBatchTransactionList(string chainId, long startBlockHeight,
    //     long endBlockHeight)
    // {
    //     var batchList = new List<IndexerTransactionDto>();
    //
    //     Stopwatch stopwatch = Stopwatch.StartNew();
    //
    //     var tasks = new List<Task>();
    //     for (long i = startBlockHeight; i <= endBlockHeight; i += 100)
    //     {
    //         var start = i;
    //         var end = start + 99 > endBlockHeight ? endBlockHeight : start + 99;
    //         var data = await _aelfIndexerProvider.GetTransactionsAsync(chainId, start, end, "");
    //         if (data.IsNullOrEmpty())
    //         {
    //             _logger.LogError("Get batch transaction list is null,chainId:{0},start:{1},end:{2}",
    //                 chainId, start, end);
    //             continue;
    //         }
    //
    //         batchList.AddRange(data);
    //     }
    //
    //     await tasks.WhenAll();
    //
    //     stopwatch.Stop();
    //     _logger.LogInformation("Get batch transaction list from chainId:{0},start:{1},end:{2},count:{3},time:{4}",
    //         chainId, startBlockHeight, endBlockHeight, batchList.Count, stopwatch.Elapsed.TotalSeconds);
    //     return batchList;
    // }

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