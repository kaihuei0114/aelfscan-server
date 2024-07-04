using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.EntityMapping.Repositories;
using AElf.Standards.ACS0;
using AElf.Types;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.EsIndex;
using AElfScanServer.Worker.Core.Provider;
using Elasticsearch.Net;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Dtos.Indexer;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Options;
using AElfScanServer.HttpApi.Provider;
using Binance.Spot;
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
using Interval = Binance.Spot.Models.Interval;
using Math = System.Math;
using Timer = System.Timers.Timer;

namespace AElfScanServer.Worker.Core.Service;

public interface ITransactionService
{
    public Task UpdateTransactionRatePerMinuteTaskAsync();

    public Task UpdateTransactionRelatedDataTaskAsync();

    public Task UpdateDailyNetwork();


    public Task BatchUpdateNodeNetworkTask();


    public Task UpdateElfPrice();

    public Task BatchPullTransactionTask();
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
    private readonly IEntityMappingRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly IEntityMappingRepository<ElfPriceIndex, string> _priceRepository;
    private readonly IEntityMappingRepository<NodeBlockProduceIndex, string> _nodeBlockProduceRepository;
    private readonly IEntityMappingRepository<DailyBlockProduceCountIndex, string> _blockProduceRepository;
    private readonly IEntityMappingRepository<DailyBlockProduceDurationIndex, string> _blockProduceDurationRepository;
    private readonly IEntityMappingRepository<DailyCycleCountIndex, string> _cycleCountRepository;


    private readonly IEntityMappingRepository<DailyAvgTransactionFeeIndex, string> _avgTransactionFeeRepository;
    private readonly IEntityMappingRepository<DailyAvgBlockSizeIndex, string> _avgBlockSizeRepository;
    private readonly IEntityMappingRepository<DailyBlockRewardIndex, string> _blockRewardRepository;
    private readonly IEntityMappingRepository<DailyTotalBurntIndex, string> _totalBurntRepository;
    private readonly IEntityMappingRepository<DailyDeployContractIndex, string> _deployContractRepository;


    private readonly IEntityMappingRepository<DailyTransactionCountIndex, string> _transactionCountRepository;
    private readonly IEntityMappingRepository<DailyUniqueAddressCountIndex, string> _uniqueAddressRepository;
    private readonly IEntityMappingRepository<DailyActiveAddressCountIndex, string> _activeAddressRepository;
    private readonly IEntityMappingRepository<DailyJobExecuteIndex, string> _JobExecuteIndexRepository;
    private readonly ILogger<TransactionService> _logger;
    private static bool FinishInitChartData = false;
    private static int BatchPullRoundCount = 1;
    private static object _lock = new object();

    private static Timer timer;
    private static long PullTransactioninterval = 5000 - 1;


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
        IEntityMappingRepository<DailyCycleCountIndex, string> cycleCountRepository,
        IEntityMappingRepository<TransactionIndex, string> transactionIndexRepository,
        IEntityMappingRepository<ElfPriceIndex, string> priceRepository,
        IEntityMappingRepository<DailyAvgTransactionFeeIndex, string> avgTransactionFeeRepository,
        IEntityMappingRepository<DailyAvgBlockSizeIndex, string> avgBlockSizeRepository,
        IEntityMappingRepository<DailyBlockRewardIndex, string> blockRewardRepository,
        IEntityMappingRepository<DailyTotalBurntIndex, string> totalBurntRepository,
        IEntityMappingRepository<DailyDeployContractIndex, string> deployContractRepository,
        IEntityMappingRepository<DailyTransactionCountIndex, string> transactionCountRepository,
        IEntityMappingRepository<DailyUniqueAddressCountIndex, string> uniqueAddressRepository,
        IEntityMappingRepository<DailyActiveAddressCountIndex, string> activeAddressRepository,
        IEntityMappingRepository<DailyJobExecuteIndex, string> jobExecuteIndexRepository) :
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
        var settings = new ConnectionSettings(connectionPool).DisableDirectStreaming();
        _elasticClient = new ElasticClient(settings);
        _workerOptions = workerOptions;
        _roundIndexRepository = roundIndexRepository;
        _nodeBlockProduceRepository = nodeBlockProduceRepository;
        _blockProduceRepository = blockProduceRepository;
        _blockProduceDurationRepository = blockProduceDurationRepository;
        _cycleCountRepository = cycleCountRepository;
        _transactionIndexRepository = transactionIndexRepository;
        _priceRepository = priceRepository;
        EsIndex.SetElasticClient(_elasticClient);
        _avgTransactionFeeRepository = avgTransactionFeeRepository;
        _avgBlockSizeRepository = avgBlockSizeRepository;
        _blockRewardRepository = blockRewardRepository;
        _totalBurntRepository = totalBurntRepository;
        _deployContractRepository = deployContractRepository;
        _transactionCountRepository = transactionCountRepository;
        _uniqueAddressRepository = uniqueAddressRepository;
        _activeAddressRepository = activeAddressRepository;
        _JobExecuteIndexRepository = jobExecuteIndexRepository;
    }


    public async Task UpdateElfPrice()
    {
        try
        {
            var market = new Market();
            var data1 =
                await market.KlineCandlestickData("ELFUSDT", Interval.ONE_DAY, 1631030400000, 1662566400000);


            var data2 =
                await market.KlineCandlestickData("ELFUSDT", Interval.ONE_DAY, 1662566400000, 1694102400000);

            var data3 =
                await market.KlineCandlestickData("ELFUSDT", Interval.ONE_DAY, 1694102400000, 1725724800000);

            List<string[]> dataList1 = JsonConvert.DeserializeObject<List<string[]>>(data1);

            List<string[]> dataList2 = JsonConvert.DeserializeObject<List<string[]>>(data2);

            List<string[]> dataList3 = JsonConvert.DeserializeObject<List<string[]>>(data3);


            var dataList = dataList1.Concat(dataList2).Concat(dataList3).ToList();

            var batch = new List<ElfPriceIndex>();
            foreach (var strings in dataList)
            {
                var elfPriceIndex = new ElfPriceIndex()
                {
                    OpenTime = long.Parse(strings[0]),
                    Open = strings[1],
                    High = strings[2],
                    Low = strings[3],
                    Close = strings[3],
                };
                elfPriceIndex.DateStr = DateTimeHelper.GetDateTimeString(elfPriceIndex.OpenTime);
                batch.Add(elfPriceIndex);
            }


            await _priceRepository.AddOrUpdateManyAsync(batch);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "UpdateElfPrice err");
        }
    }

    public async Task BatchPullTransactionTask()
    {
        if (!FinishInitChartData)
        {
            await ConnectAsync();
            foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
            {
                RedisDatabase.KeyDelete(RedisKeyHelper.TransactionLastBlockHeight(chainId));
                RedisDatabase.KeyDelete(RedisKeyHelper.AddressSet(chainId));
            }
        
            FinishInitChartData = true;
        }

        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            try
            {
                await ConnectAsync();
                var redisValue = RedisDatabase.StringGet(RedisKeyHelper.TransactionLastBlockHeight(chainId));
                var lastBlockHeight = redisValue.IsNullOrEmpty ? 1 : long.Parse(redisValue) + 1;

                Stopwatch stopwatch = Stopwatch.StartNew();
                _logger.LogInformation("BatchPullTransactionTask:{e} start,startBlockHeight:{s1},endBlockHeight:{s2}",
                    chainId, lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);
                var batchTransactionList =
                    await GetBatchTransactionList(chainId, lastBlockHeight, lastBlockHeight + PullTransactioninterval);

                if (batchTransactionList.IsNullOrEmpty())
                {
                    _logger.LogInformation("Find transaction list:chainId{c}, start:{s},end:{e}", chainId,
                        lastBlockHeight,
                        lastBlockHeight + PullTransactioninterval);
                }


                var dateSet = new HashSet<string>();

                batchTransactionList = batchTransactionList.OrderBy(c => c.BlockHeight).Select(s =>
                {
                    var totalMilliseconds = DateTimeHelper.GetTotalMilliseconds(s.BlockTime);
                    if (totalMilliseconds == 0 && s.BlockHeight == 1)
                    {
                        s.DateStr = _globalOptions.CurrentValue.OneBlockTime[chainId];
                        s.BlockTime =
                            DateTimeHelper.GetDateTimeFromYYMMDD(_globalOptions.CurrentValue.OneBlockTime[chainId]);
                    }
                    else
                    {
                        s.DateStr = DateTimeHelper.GetDateStr(s.BlockTime);
                    }

                    dateSet.Add(s.DateStr);
                    return s;
                }).ToList();


                await _transactionIndexRepository.AddOrUpdateManyAsync(batchTransactionList);
                stopwatch.Stop();


                RedisDatabase.StringSet(RedisKeyHelper.TransactionLastBlockHeight(chainId),
                    lastBlockHeight + PullTransactioninterval);


                foreach (var s in dateSet)
                {
                    var dailyJobExecuteIndex = new DailyJobExecuteIndex()
                    {
                        ChainId = chainId,
                        IsStatistic = false,
                        DataWriteFinishTime = DateTime.UtcNow,
                        DateStr = s
                    };

                    await _JobExecuteIndexRepository.AddOrUpdateAsync(dailyJobExecuteIndex);
                }

                _logger.LogInformation(
                    "BatchPullTransactionTask:{e} end date:{d},count:{1},time:{2},startBlockHeight:{s1},endBlockHeight:{s2}",
                    dateSet.ToList(),
                    chainId, batchTransactionList.Count, stopwatch.Elapsed.TotalSeconds, lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "BatchPullTransactionTask err:{c}", chainId);
            }
        }
    }

    public async Task UpdateDailyTransactionData(List<string> DateStrList, string chainId)
    {
        if (DateStrList.IsNullOrEmpty())
        {
            _logger.LogInformation("Date str list is null:{c}", chainId);
            return;
        }

        var query = await _transactionIndexRepository.GetQueryableAsync();
        query = query.Where(c => c.ChainId == chainId).Take(10000);
        foreach (var date in DateStrList)
        {
            var queryableAsync = await _priceRepository.GetQueryableAsync();
            var elfPriceIndices = queryableAsync.Where(c => c.DateStr == date).ToList();
            double elfPrice = 0;
            if (elfPriceIndices.Count > 0)
            {
                elfPrice = double.Parse(elfPriceIndices[0].Close);
            }


            var totalMilliseconds = DateTimeHelper.ConvertYYMMDD(date);
            var dayHourList = DateTimeHelper.GetDateTimeHourList(date);
            var dailyAvgTransactionFeeIndex = new DailyAvgTransactionFeeIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };
            var dailyBlockRewardIndex = new DailyBlockRewardIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };

            var dailyDeployContractBurntIndex = new DailyDeployContractIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };

            var dailyTotalBurntIndex = new DailyTotalBurntIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };

            var dailyTransactionCountIndex = new DailyTransactionCountIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };

            var dailyUniqueAddressCountIndex = new DailyUniqueAddressCountIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };

            var dailyActiveAddressCountIndex = new DailyActiveAddressCountIndex()
            {
                ChainId = chainId,
                Date = totalMilliseconds,
                DateStr = date
            };
            var totalBurnt = 0L;

            var blockSet = new HashSet<long>();
            var addressSet = new HashSet<string>();

            var addressFromSet = new HashSet<string>();

            var addressToSet = new HashSet<string>();
            decimal totalReward = 0l;
            var totalFee = 0l;


            for (var i = 0; i < dayHourList.Count - 1; i++)
            {
                var transactionIndexList = query.Where(c => c.BlockTime >= dayHourList[i])
                    .Where(c => c.BlockTime < dayHourList[i + 1]).ToList();

                if (transactionIndexList.IsNullOrEmpty())
                {
                    _logger.LogInformation("Transaction index list is null:{c},{d}", chainId, date);
                    continue;
                }


                foreach (var transactionIndex in transactionIndexList)
                {
                    addressFromSet.Add(transactionIndex.From);
                    addressToSet.Add(transactionIndex.To);
                    addressSet.Add(transactionIndex.From);
                    addressSet.Add(transactionIndex.To);

                    if (!blockSet.Contains(transactionIndex.BlockHeight))
                    {
                        var milliseconds = DateTimeHelper.GetTotalMilliseconds(transactionIndex.BlockTime);

                        if (milliseconds < _globalOptions.CurrentValue.NextTermDate)
                        {
                            totalReward += (decimal)0.125;
                        }
                        else
                        {
                            totalReward += _globalOptions.CurrentValue.NextTermReward;
                        }

                        blockSet.Add(transactionIndex.BlockHeight);
                    }

                    foreach (var txLogEvent in transactionIndex.LogEvents)
                    {
                        var logEvent = LogEventHelper.ParseLogEventExtraProperties(txLogEvent.ExtraProperties);
                        switch (txLogEvent.EventName)
                        {
                            case nameof(ContractDeployed):
                                dailyDeployContractBurntIndex.Count++;
                                break;

                            case nameof(Burned):
                                var burned = new Burned();
                                burned.MergeFrom(logEvent);
                                var burnt = LogEventHelper.ParseBurnt(burned.Amount, burned.Burner.ToBase58(),
                                    burned.Symbol,
                                    transactionIndex.ChainId);
                                if (burnt > 0)
                                {
                                    dailyTotalBurntIndex.HasBurntBlockCount++;
                                    totalBurnt += burnt;
                                }

                                break;
                        }
                    }

                    totalFee += LogEventHelper.ParseTransactionFees(transactionIndex.ExtraProperties);
                }

                dailyAvgTransactionFeeIndex.TransactionCount += transactionIndexList.Count;
            }

            var totalFeeDouble = ((double)totalFee / 1e8);

            dailyAvgTransactionFeeIndex.TotalFeeElf = totalFeeDouble.ToString();

            dailyAvgTransactionFeeIndex.AvgFeeElf =
                (totalFeeDouble / dailyAvgTransactionFeeIndex.TransactionCount).ToString();
            dailyAvgTransactionFeeIndex.AvgFeeUsdt = ((totalFeeDouble / dailyAvgTransactionFeeIndex.TransactionCount) *
                                                      elfPrice).ToString();

            dailyBlockRewardIndex.TotalBlockCount = blockSet.Count;
            dailyBlockRewardIndex.BlockReward = totalReward.ToString();

            dailyTotalBurntIndex.Burnt = ((double)totalBurnt / 1e8).ToString();

            dailyTransactionCountIndex.TransactionCount = dailyAvgTransactionFeeIndex.TransactionCount;
            dailyTransactionCountIndex.BlockCount = blockSet.Count;

            dailyActiveAddressCountIndex.AddressCount = addressSet.Count;
            dailyActiveAddressCountIndex.SendAddressCount = addressFromSet.Count;
            dailyActiveAddressCountIndex.ReceiveAddressCount = addressToSet.Count;


            await ConnectAsync();
            foreach (var s in addressSet)
            {
                if (!RedisDatabase.SetContains(RedisKeyHelper.AddressSet(chainId), s))
                {
                    dailyUniqueAddressCountIndex.AddressCount++;
                    RedisDatabase.SetAdd(RedisKeyHelper.AddressSet(chainId), s);
                }
            }

            var totalAddress = RedisDatabase.SetLength(RedisKeyHelper.AddressSet(chainId));
            dailyUniqueAddressCountIndex.TotalUniqueAddressees = (int)totalAddress;

            await _avgTransactionFeeRepository.AddOrUpdateAsync(dailyAvgTransactionFeeIndex);
            await _blockRewardRepository.AddOrUpdateAsync(dailyBlockRewardIndex);
            await _totalBurntRepository.AddOrUpdateAsync(dailyTotalBurntIndex);
            await _deployContractRepository.AddAsync(dailyDeployContractBurntIndex);
            await _transactionCountRepository.AddOrUpdateAsync(dailyTransactionCountIndex);
            await _uniqueAddressRepository.AddOrUpdateAsync(dailyUniqueAddressCountIndex);
            await _activeAddressRepository.AddOrUpdateAsync(dailyActiveAddressCountIndex);
            _logger.LogInformation("Update daily transaction data,chainId:{c} date:{d},", chainId, date);
        }
    }


    public async Task UpdateDailyNetwork()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            try
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
            catch (Exception e)
            {
                _logger.LogError(e, "UpdateDailyNetwork err:{c}", chainId);
                throw;
            }
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

    public async Task BatchUpdateNodeNetworkTask()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                stopwatch.Start();
                var tasks = new List<Task>();

                await ConnectAsync();
                var redisValue = RedisDatabase.StringGet(RedisKeyHelper.LatestRound(chainId));
                if (redisValue.IsNullOrEmpty)
                {
                    _logger.LogError("BatchUpdateNetwork redisValue is null chainId:{c}", chainId);
                    return;
                }

                var startRoundNumber = (long)redisValue;

                var rounds = new List<Round>();

                var _lock = new object();

                try
                {
                    for (long i = startRoundNumber; i < startRoundNumber + BatchPullRoundCount; i++)
                    {
                        tasks.Add(GetRound(i, chainId).ContinueWith(task =>
                        {
                            lock (_lock)
                            {
                                rounds.Add(task.Result);
                            }
                        }));
                    }

                    await Task.WhenAll(tasks);
                }
                catch (Exception e)
                {
                    _logger.LogInformation("Get round err,{e}", e.Message);
                    Thread.Sleep(1000 * 20);
                }

                stopwatch.Stop();
                var findCost = stopwatch.Elapsed.TotalSeconds;
                var roundIndices = new List<RoundIndex>();
                var nodeBlockProduceIndices = new List<NodeBlockProduceIndex>();


                foreach (var round in rounds)
                {
                    var statisticRound = await StatisticRound(round, chainId);
                    roundIndices.Add(statisticRound.r);
                    nodeBlockProduceIndices.AddRange(statisticRound.n);
                }

                stopwatch = Stopwatch.StartNew();


                await _roundIndexRepository.AddOrUpdateManyAsync(roundIndices);
                await _nodeBlockProduceRepository.AddOrUpdateManyAsync(nodeBlockProduceIndices);
                _logger.LogInformation("Insert batch round index chainId:{0},round number:{1},date:{2}", chainId,
                    startRoundNumber, DateTimeHelper.GetDateTimeString(roundIndices.First().StartTime));
                stopwatch.Stop();
                var insertCost = stopwatch.Elapsed.TotalSeconds;
                _logger.LogInformation(
                    "BatchUpdateNetwork cost time,round index find cost time:{t},insert cost time:{t2},start:{s1},end:{s2},chainId:{c},,round count:{n},node produce count:{c2}",
                    findCost, insertCost, chainId, startRoundNumber, startRoundNumber + BatchPullRoundCount - 1,
                    roundIndices.Count, nodeBlockProduceIndices.Count);

                RedisDatabase.StringSet(RedisKeyHelper.LatestRound(chainId), startRoundNumber + BatchPullRoundCount);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "BatchUpdateNodeNetworkTask err:{c}", chainId);
            }
        }
    }

    internal async Task<Round> GetRound(long roundNumber, string chainId)
    {
        var client = new AElfClient(_globalOptions.CurrentValue.ChainNodeHosts[chainId]);

        var param = new Int64Value()
        {
            Value = roundNumber
        };


        var transaction = await client.GenerateTransactionAsync(
            client.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
            _globalOptions.CurrentValue.ContractAddressConsensus[chainId],
            "GetRoundInformation", param);


        var signTransaction = client.SignTransaction(GlobalOptions.PrivateKey, transaction);

        var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto()
        {
            RawTransaction = HexByteConvertorExtensions.ToHex(signTransaction.ToByteArray())
        });

        var round = Round.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(result));
        return round;
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

    internal async Task<(RoundIndex r, List<NodeBlockProduceIndex> n)> StatisticRound(Round round, string chainId)
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


        return (roundIndex, batch);
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

    public async Task UpdateTransactionRelatedDataTaskAsync()
    {
        var query = await _JobExecuteIndexRepository.GetQueryableAsync();
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            try
            {
                var jobList = query.Where(c => c.ChainId == chainId).Take(10000).ToList();
                jobList = jobList.OrderBy(c => c.DateStr).ToList();
                if (jobList.Count <= 1)
                {
                    continue;
                }


                for (var i = 0; i < jobList.Count - 1; i++)
                {
                    if (!jobList[i].IsStatistic)
                    {
                        jobList[i].StatisticStartTime = DateTime.UtcNow;
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        await UpdateDailyTransactionData(new List<string>() { jobList[i].DateStr }, chainId);
                        stopwatch.Stop();
                        jobList[i].IsStatistic = true;
                        jobList[i].CostTime = stopwatch.Elapsed.TotalSeconds;
                        await _JobExecuteIndexRepository.AddOrUpdateAsync((jobList[i]));
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "UpdateTransactionRelatedDataTaskAsync err:chainId{c}", chainId);
            }
        }
    }


    public async Task<List<TransactionIndex>> GetBatchTransactionList(string chainId, long startBlockHeight,
        long endBlockHeight)
    {
        object _lock = new object();
        var batchList = new List<TransactionIndex>();

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


    public async Task UpdateTransactionRatePerMinuteTaskAsync()
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