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
        IEntityMappingRepository<DailyActiveAddressCountIndex, string> activeAddressRepository) :
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
    }


    public async Task UpdateElfPrice()
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

    public async Task BatchPullTransactionTask()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.TransactionLastBlockHeight(chainId));
            var lastBlockHeight = redisValue.IsNullOrEmpty ? 1 : long.Parse(redisValue) + 1;

            Stopwatch stopwatch = Stopwatch.StartNew();
            var batchTransactionList =
                await GetBatchTransactionList(chainId, lastBlockHeight, lastBlockHeight + PullTransactioninterval);

            if (batchTransactionList.IsNullOrEmpty())
            {
                _logger.LogInformation("Find transaction list:chainId{c}, start:{s},end:{e}", chainId, lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);
            }


            var dateSet = new HashSet<string>();

            batchTransactionList = batchTransactionList.OrderBy(c => c.BlockHeight).Select(s =>
            {
                var totalMilliseconds = DateTimeHelper.GetTotalMilliseconds(s.BlockTime);
                if (totalMilliseconds == 0 && s.BlockHeight == 1)
                {
                    s.DateStr = DateTimeHelper.GetDateTimeString(_globalOptions.CurrentValue.OneBlockTime[chainId]);
                }
                else
                {
                    s.DateStr = DateTimeHelper.GetDateStr(s.BlockTime);
                }

                if (s.DateStr == "2020-10-18")
                {
                    _logger.LogInformation("");
                }

                dateSet.Add(s.DateStr);
                return s;
            }).ToList();


            await _transactionIndexRepository.AddOrUpdateManyAsync(batchTransactionList);
            stopwatch.Stop();
            _logger.LogInformation(
                "Find transaction list chainId:{0},count:{1},time:{2},startBlockHeight:{s1},endBlockHeight:{s2}",
                chainId,
                batchTransactionList.Count, stopwatch.Elapsed.TotalSeconds, lastBlockHeight,
                lastBlockHeight + PullTransactioninterval);

            RedisDatabase.StringSet(RedisKeyHelper.TransactionLastBlockHeight(chainId),
                lastBlockHeight + PullTransactioninterval);


            var dateList = dateSet.ToList().OrderBy(c => c).ToList();

            var stringGet = RedisDatabase.StringGet(RedisKeyHelper.LastTransactionDate(chainId));
            if (!stringGet.IsNullOrEmpty)
            {
                var lastDate = stringGet.ToString();

                if (dateList.Count >= 2)
                {
                    await UpdateDailyTransactionData(new List<string>() { lastDate }, chainId);
                    RedisDatabase.StringSet(RedisKeyHelper.LastTransactionDate(chainId), dateList[1]);
                }
            }
            else
            {
                RedisDatabase.StringSet(RedisKeyHelper.LastTransactionDate(chainId), dateList[0]);
            }
        }
    }

    public async Task UpdateDailyTransactionData(List<string> DateStrList, string chainId)
    {
        Thread.Sleep(1000 * 6);
        if (DateStrList.IsNullOrEmpty())
        {
            _logger.LogInformation("Date str list is null:{c}", chainId);
            return;
        }

        foreach (var date in DateStrList)
        {
            var transactionIndexList = await EsIndex.GetTransactionIndexList(chainId, date);
            if (transactionIndexList.IsNullOrEmpty())
            {
                _logger.LogInformation("Transaction index list is null:{c},{d}", chainId, date);
                continue;
            }

            var totalMilliseconds = DateTimeHelper.ConvertYYMMDD(date);

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

            var queryableAsync = await _priceRepository.GetQueryableAsync();

            var priceIndices = queryableAsync.ToList();
            var elfPriceIndices = queryableAsync.Where(c => c.DateStr == date).ToList();


            double elfPrice = 0;
            if (elfPriceIndices.Count > 0)
            {
                elfPrice = double.Parse(elfPriceIndices[0].Close);
            }

            var totalBurnt = 0L;

            var blockSet = new HashSet<long>();
            var addressSet = new HashSet<string>();

            var addressFromSet = new HashSet<string>();

            var addressToSet = new HashSet<string>();
            decimal totalReward = 0l;
            var totalFee = 0l;

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

            var totalFeeDouble = ((double)totalFee / 1e8);

            dailyAvgTransactionFeeIndex.TotalFeeElf = totalFeeDouble.ToString();
            dailyAvgTransactionFeeIndex.TransactionCount = transactionIndexList.Count;
            dailyAvgTransactionFeeIndex.AvgFeeElf =
                (totalFeeDouble / transactionIndexList.Count).ToString();
            dailyAvgTransactionFeeIndex.AvgFeeUsdt = ((totalFeeDouble / transactionIndexList.Count) *
                                                      elfPrice).ToString();

            dailyBlockRewardIndex.TotalBlockCount = blockSet.Count;
            dailyBlockRewardIndex.BlockReward = totalReward.ToString();

            dailyTotalBurntIndex.Burnt = ((double)totalBurnt / 1e8).ToString();

            dailyTransactionCountIndex.TransactionCount = transactionIndexList.Count;
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
            _logger.LogInformation("Update daily transaction data,date:{d},chainId:{c}", date, chainId);
        }
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

    public async Task BatchUpdateNodeNetworkTask()
    {
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
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
        await ConnectAsync();


        await UpdateDailyTransactionData(new List<string>(){"2020-10-17"}, "AELF");
    }


    public async Task HandlerDailyActiveAddressesAsync(List<TransactionIndex> list, string chainId)
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

    public async Task HandlerUniqueAddressesAsync(List<TransactionIndex> list, string chainId)
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

            var firstUniqueAddressCounts = dic.Select(c => new DailyUniqueAddressCount()
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


        var updateUniqueAddressCounts = JsonConvert.DeserializeObject<List<DailyUniqueAddressCount>>(stringGet);

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
                updateAddressCountsDic[keyValuePair.Value] = new DailyUniqueAddressCount()
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


    public async Task HandlerDailyTransactionsAsync(List<TransactionIndex> list, string chainId)
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

            var dailyTransactionCounts = updateDailyTransactionCounts.OrderBy(c => c.Date).ToList();

            var d = JsonConvert.SerializeObject(dailyTransactionCounts);

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
                    "Update daily transaction count date:{d},transaction count:{c1},block count:{c2},start:{s1},end:{s2}",
                    DateTimeHelper.GetDateTimeString(date), v.TransactionCount, v.BlockCount, list[0].BlockHeight,
                    list.Last().BlockHeight);
            }
            else
            {
                updateTransactionCountsDic[date] = new DailyTransactionCount()
                {
                    TransactionCount = nowDailyTransactionCountDic[date],
                    BlockCount = nowDailyBlockCountDic[date].Count,
                    Date = date,
                    DateStr = DateTimeHelper.GetDateTimeString(date)
                };
                _logger.LogInformation(
                    "Add daily transaction count date:{d},transaction count:{c1},block count:{c2},start:{s1},end:{s2}",
                    DateTimeHelper.GetDateTimeString(keyValuePair.Key), nowDailyTransactionCountDic[date],
                    nowDailyBlockCountDic[date].Count, list[0].BlockHeight,
                    list.Last().BlockHeight);
            }
        }

        var transactionCounts = updateTransactionCountsDic.Values.Where(c => c.Date > 0).OrderBy(c => c.Date).ToList();

        var serializeObject = JsonConvert.SerializeObject(transactionCounts);

        RedisDatabase.StringSet(RedisKeyHelper.DailyTransactionCount(chainId), serializeObject);
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