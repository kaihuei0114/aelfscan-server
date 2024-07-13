using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Vote;
using AElf.CSharp.Core.Extension;
using AElf.EntityMapping.Repositories;
using AElf.Standards.ACS0;
using AElf.Types;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.EsIndex;
using AElfScanServer.Worker.Core.Provider;
using Elasticsearch.Net;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.NodeProvider;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Dtos.Indexer;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Options;
using AElfScanServer.HttpApi.Provider;
using Aetherlink.PriceServer;
using Aetherlink.PriceServer.Dtos;
using Binance.Spot;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nest;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Nito.AsyncEx;
using StackExchange.Redis;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using AddressIndex = AElfScanServer.Common.Dtos.ChartData.AddressIndex;
using Interval = Binance.Spot.Models.Interval;
using Math = System.Math;
using Timer = System.Timers.Timer;
using VotedItems = AElf.Client.Vote.VotedItems;

namespace AElfScanServer.Worker.Core.Service;

public interface ITransactionService
{
    public Task UpdateTransactionRatePerMinuteTaskAsync();


    public Task UpdateDailyNetwork();


    public Task BatchUpdateNodeNetworkTask();


    public Task UpdateElfPrice();

    public Task BatchPullTransactionTask();


    public Task BlockSizeTask();
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
    private readonly IEntityMappingRepository<DailyAvgBlockSizeIndex, string> _blockSizeRepository;
    private readonly IEntityMappingRepository<DailyTransactionRecordIndex, string> _recordIndexRepository;
    private readonly IEntityMappingRepository<DailyHasFeeTransactionIndex, string> _hasFeeTransactionRepository;
    private readonly IEntityMappingRepository<DailyContractCallIndex, string> _dailyContractCallRepository;
    private readonly IEntityMappingRepository<DailyTotalContractCallIndex, string> _dailyTotalContractCallRepository;

    private readonly IEntityMappingRepository<DailyMarketCapIndex, string> _dailyMarketCapIndexRepository;
    private readonly IEntityMappingRepository<DailySupplyGrowthIndex, string> _dailySupplyGrowthIndexRepository;
    private readonly IEntityMappingRepository<DailyStakedIndex, string> _dailyStakedIndexRepository;
    private readonly IEntityMappingRepository<DailyVotedIndex, string> _dailyVotedIndexRepository;
    private readonly IEntityMappingRepository<TransactionErrInfoIndex, string> _transactionErrInfoIndexRepository;
    private readonly IEntityMappingRepository<DailySupplyChange, string> _dailySupplyChangeRepository;
    private readonly IPriceServerProvider _priceServerProvider;


    private readonly IEntityMappingRepository<AddressIndex, string> _addressRepository;
    private readonly NodeProvider _nodeProvider;

    private readonly ILogger<TransactionService> _logger;
    private static bool FinishInitChartData = false;
    private static int BatchPullRoundCount = 1;
    private static int BlockSizeInterval = 1;
    private static long BpStakedAmount = 100000;
    private static object _lock = new object();

    private static Timer timer;
    private static long PullTransactioninterval = 4000 - 1;


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
        IEntityMappingRepository<DailyTransactionRecordIndex, string> recordIndexRepository,
        NodeProvider nodeProvide,
        IEntityMappingRepository<DailyAvgBlockSizeIndex, string> blockSizeRepository,
        IEntityMappingRepository<AddressIndex, string> addressRepository,
        IEntityMappingRepository<DailyHasFeeTransactionIndex, string> hasFeeTransactionRepository,
        IEntityMappingRepository<DailyContractCallIndex, string> dailyContractCallRepository,
        IEntityMappingRepository<DailyTotalContractCallIndex, string> dailyTotalContractCallRepository,
        IEntityMappingRepository<DailyMarketCapIndex, string> dailyMarketCapIndexRepository,
        IEntityMappingRepository<DailySupplyGrowthIndex, string> dailySupplyGrowthIndexRepository,
        IEntityMappingRepository<DailyStakedIndex, string> dailyStakedIndexRepository,
        IEntityMappingRepository<DailyVotedIndex, string> dailyVotedIndexRepository,
        IEntityMappingRepository<TransactionErrInfoIndex, string> transactionErrInfoIndexRepository,
        IEntityMappingRepository<DailySupplyChange, string> dailySupplyChangeRepository,
        IPriceServerProvider priceServerProvider) :
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
        _recordIndexRepository = recordIndexRepository;
        _blockSizeRepository = blockSizeRepository;
        _nodeProvider = nodeProvide;
        _addressRepository = addressRepository;
        _hasFeeTransactionRepository = hasFeeTransactionRepository;
        _dailyContractCallRepository = dailyContractCallRepository;
        _dailyTotalContractCallRepository = dailyTotalContractCallRepository;
        _dailyMarketCapIndexRepository = dailyMarketCapIndexRepository;
        _dailySupplyGrowthIndexRepository = dailySupplyGrowthIndexRepository;
        _priceServerProvider = priceServerProvider;
        _dailyStakedIndexRepository = dailyStakedIndexRepository;
        _dailyVotedIndexRepository = dailyVotedIndexRepository;
        _transactionErrInfoIndexRepository = transactionErrInfoIndexRepository;
        _dailySupplyChangeRepository = dailySupplyChangeRepository;
    }

    public async Task BlockSizeTask()
    {
        _logger.LogInformation("start BlockSizeTask");

        var tasks = new List<Task>();

        foreach (var chanId in _globalOptions.CurrentValue.ChainIds)
        {
            tasks.Add(BatchPullBlockSize(chanId));
        }

        await tasks.WhenAll();
    }


    public async Task BatchPullBlockSize(string chainId)
    {
        var dic = new Dictionary<string, DailyAvgBlockSizeIndex>();
        await ConnectAsync();
        var redisValue = RedisDatabase.StringGet(RedisKeyHelper.BlockSizeLastBlockHeight(chainId));
        var lastBlockHeight = redisValue.IsNullOrEmpty ? 0 : long.Parse(redisValue);
        while (true)
        {
            try
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var tasks = new List<Task>();
            var blockSizeIndices = new List<BlockSizeDto>();
            var _lock = new object();

            var startNew = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < BlockSizeInterval; i++)
                {
                    lastBlockHeight++;
                    tasks.Add(_nodeProvider.GetBlockSize(chainId, lastBlockHeight).ContinueWith(task =>
                    {
                        lock (_lock)
                        {
                            if (task.Result != null)
                            {
                                blockSizeIndices.Add(task.Result);
                            }
                        }
                    }));
                }
            }
            catch (Exception e)
            {
                _logger.LogError("BatchPullBlockSize err:{c},err msg:{e},startBlockHeight:{s1},endBlockHeight:{s2}",
                    chainId, e.Message, lastBlockHeight - BlockSizeInterval,
                    lastBlockHeight);
                break;
            }

            await tasks.WhenAll();
            foreach (var blockSize in blockSizeIndices)
            {
                if (blockSize.Header == null)
                {
                    _logger.LogInformation("Block size index header is null:{c}", chainId);
                    continue;
                }


                string date = "";
                if (long.Parse(blockSize.Header.Height) == 1)
                {
                    date = _globalOptions.CurrentValue.OneBlockTime[chainId];
                }
                else
                {
                    date = DateTimeHelper.FormatDateStr(blockSize.Header.Time);

                    if (date == DateTimeHelper.GetDateStr(DateTime.UtcNow))
                    {
                        break;
                    }
                }

                if (dic.TryGetValue(date, out var v))
                {
                    v.TotalSize += blockSize.BlockSize;
                    v.EndBlockHeight = Math.Max(int.Parse(blockSize.Header.Height), v.EndBlockHeight);
                    v.StartBlockHeight = Math.Min(int.Parse(blockSize.Header.Height), v.StartBlockHeight);
                    v.BlockCount++;
                }
                else
                {
                    dic[date] = new DailyAvgBlockSizeIndex()
                    {
                        ChainId = chainId,
                        DateStr = date,
                        TotalSize = blockSize.BlockSize,
                        StartBlockHeight = int.Parse(blockSize.Header.Height),
                        StartTime = DateTime.UtcNow,
                        EndBlockHeight = int.Parse(blockSize.Header.Height),
                        BlockCount = 1
                    };
                }
            }

            startNew.Stop();
            _logger.LogInformation(
                "BatchPullBlockSize :{c},count:{1},time:{2},startBlockHeight:{s1},endBlockHeight:{s2}",
                chainId, blockSizeIndices.Count, startNew.Elapsed.TotalSeconds, lastBlockHeight - BlockSizeInterval,
                lastBlockHeight);
            if (dic.Count >= 2)
            {
                var sizeIndices = dic.Values.OrderBy(c => c.DateStr).ToList();

                var blockSizeIndex = sizeIndices[0];
                blockSizeIndex.AvgBlockSize = (blockSizeIndex.TotalSize / blockSizeIndex.BlockCount).ToString();
                blockSizeIndex.EndTime = DateTime.UtcNow;
                blockSizeIndex.Date = DateTimeHelper.ConvertYYMMDD(blockSizeIndex.DateStr);
                await _blockSizeRepository.AddOrUpdateAsync(sizeIndices[0]);
                RedisDatabase.StringSet(RedisKeyHelper.BlockSizeLastBlockHeight(chainId),
                    sizeIndices[0].EndBlockHeight);
                dic.Remove(blockSizeIndex.DateStr);
            }

            lastBlockHeight++;
        }
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
            _logger.LogError("UpdateElfPrice err:{e}", e.Message);
        }
    }


    public async Task BatchPullTransactionTask()
    {
        await ConnectAsync();

        if (_globalOptions.CurrentValue.NeedInitLastHeight && !FinishInitChartData)
        {
            foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
            {
                await ConnectAsync();
                RedisDatabase.KeyDelete(RedisKeyHelper.TransactionLastBlockHeight(chainId));
            }

            FinishInitChartData = true;
        }


        var tasks = new List<Task>();
        foreach (var chainId in _globalOptions.CurrentValue.ChainIds)
        {
            tasks.Add(BatchPullTransactionJob(chainId));
        }

        await tasks.WhenAll();
    }

    public async Task BatchPullTransactionJob(string chainId)
    {
        var dic = new Dictionary<string, DailyTransactionsChartSet>();

        var lastBlockHeight = 0l;
        await ConnectAsync();
        var redisValue = RedisDatabase.StringGet(RedisKeyHelper.TransactionLastBlockHeight(chainId));
        lastBlockHeight = redisValue.IsNullOrEmpty ? 1 : long.Parse(redisValue) + 1;

        while (true)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                var batchTransactionList =
                    await GetBatchTransactionList(chainId, lastBlockHeight, lastBlockHeight + PullTransactioninterval);

                if (batchTransactionList.IsNullOrEmpty())
                {
                    continue;
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

                if (dateSet.Min(c => c) == DateTimeHelper.GetDateStr(DateTime.UtcNow))
                {
                    break;
                }


                stopwatch.Stop();
                _logger.LogInformation(
                    "BatchPullTransactionTask:{e} end date:{d},count:{1},time:{2},startBlockHeight:{s1},endBlockHeight:{s2}",
                    dateSet.ToList(),
                    chainId, batchTransactionList.Count, stopwatch.Elapsed.TotalSeconds, lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);


                var updateDailyTransactionData = await UpdateDailyTransactionData(batchTransactionList, chainId, dic);

                if (updateDailyTransactionData != null)
                {
                    await ConnectAsync();
                    RedisDatabase.StringSet(RedisKeyHelper.TransactionLastBlockHeight(chainId),
                        updateDailyTransactionData.EndBlockHeight);
                    var dailyTransactionRecordIndex = new DailyTransactionRecordIndex()
                    {
                        ChainId = chainId,
                        StartBlockHeight = updateDailyTransactionData.StartBlockHeight,
                        EndBlockHeight = updateDailyTransactionData.EndBlockHeight,
                        DateStr = updateDailyTransactionData.Date,
                        StartTime = updateDailyTransactionData.StartTime,
                        WriteCostTime = updateDailyTransactionData.CostTime,
                        DataWriteFinishTime = updateDailyTransactionData.WirteFinishiTime,
                        Id = Guid.NewGuid().ToString()
                    };

                    await _recordIndexRepository.AddOrUpdateAsync(dailyTransactionRecordIndex);
                }

                lastBlockHeight += PullTransactioninterval + 1;
            }

            catch (Exception e)
            {
                _logger.LogError(
                    "BatchPullTransactionTask err:{c},err msg:{e},startBlockHeight:{s1},endBlockHeight:{s2}", chainId,
                    e.Message, lastBlockHeight,
                    lastBlockHeight + PullTransactioninterval);
            }
        }
    }


    public async Task<DailyTransactionsChartSet> UpdateDailyTransactionData(List<TransactionIndex> transactionList,
        string chainId,
        Dictionary<string, DailyTransactionsChartSet> dic)
    {
        if (transactionList.IsNullOrEmpty())
        {
            _logger.LogInformation("Date str list is null:{c}", chainId);
            return null;
        }

        foreach (var transaction in transactionList)
        {
            var totalMilliseconds = DateTimeHelper.GetDateTotalMilliseconds(transaction.BlockTime);
            var date = DateTimeHelper.GetDateStr(transaction.BlockTime);

            var totalDeployCount = 0;
            var hasBurntBlockCount = 0;
            var totalBurnt = 0l;

            if (!dic.ContainsKey(date))
            {
                var dailyTransactionsChartSet =
                    new DailyTransactionsChartSet(transaction.ChainId, totalMilliseconds, date);
                dailyTransactionsChartSet.StartTime = DateTime.UtcNow;
                dailyTransactionsChartSet.Date = date;
                dic[date] = dailyTransactionsChartSet;
            }

            var dailyData = dic[date];
            var transactionFees = LogEventHelper.ParseTransactionFees(transaction.ExtraProperties);
            if (transactionFees > 0)
            {
                dailyData.TotalFee += LogEventHelper.ParseTransactionFees(transaction.ExtraProperties);
                dailyData.DailyAvgTransactionFeeIndex.HasFeeTransactionCount++;
                dailyData.DailyHasFeeTransactionIndex.TransactionIds.Add(transaction.TransactionId + "_" +
                                                                         transactionFees);
            }

            if (transaction.MethodName == "AnnounceElection" || transaction.MethodName == "AnnounceElectionFor")
            {
                dailyData.TotalBpStaked += BpStakedAmount;
            }
            else if (transaction.MethodName == "QuitElection")

            {
                dailyData.TotalBpStaked -= BpStakedAmount;
            }


            foreach (var txLogEvent in transaction.LogEvents)
            {
                var logEvent = LogEventHelper.ParseLogEventExtraProperties(txLogEvent.ExtraProperties);
                switch (txLogEvent.EventName)
                {
                    case nameof(ContractDeployed):
                        dailyData.DailyDeployContractIndex.Count++;
                        break;

                    case nameof(Burned):
                        var burned = new Burned();
                        burned.MergeFrom(logEvent);
                        if (burned.Symbol == "ELF")
                        {
                            var transactionId = transaction.Id + "_" + "burned" + "_" + burned.Amount / 1e8;
                            dailyData.DailySupplyChange.SupplyChange.Add(transactionId);
                            dailyData.TotalSupply -= burned.Amount;
                            dailyData.TotalBurnt += burned.Amount;
                        }

                        break;
                    case nameof(Issued):
                        var issued = new Issued();
                        issued.MergeFrom(logEvent);
                        if (issued.Symbol == "ELF")
                        {
                            var transactionId = transaction.Id + "_" + "issued" + "_" + issued.Amount / 1e8;
                            dailyData.DailySupplyChange.SupplyChange.Add(transactionId);
                            dailyData.TotalSupply += issued.Amount;
                        }

                        break;

                    case nameof(CrossChainReceived):
                        var crossChainReceived = new CrossChainReceived();
                        crossChainReceived.MergeFrom(logEvent);
                        if (crossChainReceived.Symbol == "ELF")
                        {
                            var transactionId = transaction.Id + "_" + "crossChainReceived" + "_" +
                                                crossChainReceived.Amount / 1e8;
                            dailyData.DailySupplyChange.SupplyChange.Add(transactionId);
                            dailyData.TotalSupply += crossChainReceived.Amount;
                        }

                        break;

                    case nameof(Voted):
                        var voted = new Voted();
                        voted.MergeFrom(logEvent);
                        var votedAmount = (double)voted.Amount / 1e8;
                        dailyData.TotalVotedStaked += votedAmount;
                        dailyData.DailyVotedIndexDic[voted.VoteId.ToString()] = new DailyVotedIndex()
                        {
                            ChainId = chainId,
                            Date = totalMilliseconds,
                            DateStr = date,
                            VoteId = voted.VoteId.ToString(),
                            VoteAmount = votedAmount
                        };
                        break;
                    case nameof(Withdrawn):
                        var withdrawn = new Withdrawn();
                        withdrawn.MergeFrom(logEvent);
                        dailyData.WithDrawVotedIds.Add(withdrawn.VoteId.ToString());
                        break;
                }
            }

            dailyData.AddressFromSet.Add(transaction.From);
            dailyData.AddressToSet.Add(transaction.To);
            dailyData.AddressSet.Add(transaction.From);
            dailyData.AddressSet.Add(transaction.To);
            dailyData.DailyTransactionCountIndex.TransactionCount++;
            dailyData.DailyAvgTransactionFeeIndex.TransactionCount++;
            dailyData.DailyTotalContractCallIndex.CallCount++;

            if (dailyData.DailyContractCallIndexDic.TryGetValue(transaction.To, out var v))
            {
                v.CallCount++;
                v.CallerSet.Add(transaction.From);
                dailyData.CallersDic[transaction.To].Add(transaction.From);
            }
            else
            {
                dailyData.DailyContractCallIndexDic[transaction.To] = new DailyContractCallIndex()
                {
                    Date = totalMilliseconds,
                    DateStr = date,
                    ChainId = chainId,
                    CallCount = 1,
                    ContractAddress = transaction.To,
                    CallerSet = new HashSet<string>() { transaction.From }
                };
                dailyData.CallersDic[transaction.To] = new HashSet<string>() { transaction.From };
            }

            if (dailyData.StartBlockHeight == 0)
            {
                dailyData.StartBlockHeight = transaction.BlockHeight;
            }
            else if (dailyData.EndBlockHeight == 0)
            {
                dailyData.EndBlockHeight = transaction.BlockHeight;
            }
            else if (transaction.BlockHeight > dailyData.EndBlockHeight)
            {
                dailyData.EndBlockHeight = transaction.BlockHeight;
            }
            else
            {
                continue;
            }

            if (totalMilliseconds < _globalOptions.CurrentValue.NextTermDate)
            {
                dailyData.TotalReward += (decimal)0.125;
            }
            else
            {
                dailyData.TotalReward += _globalOptions.CurrentValue.NextTermReward;
            }
        }

        if (dic.Count == 2)
        {
            var minDate = dic.Keys.Min();
            var needUpdateData = dic[minDate];


            var dailyElfPrice = await GetElfPrice(minDate);


            var totalFeeDouble = ((double)needUpdateData.TotalFee / 1e8);
            var totalBlockCount = (int)(needUpdateData.EndBlockHeight - needUpdateData.StartBlockHeight + 1);

            needUpdateData.DailyAvgTransactionFeeIndex.TotalFeeElf = totalFeeDouble.ToString("F6");

            needUpdateData.DailyAvgTransactionFeeIndex.AvgFeeElf =
                (totalFeeDouble / needUpdateData.DailyAvgTransactionFeeIndex.TransactionCount).ToString("F6");
            needUpdateData.DailyAvgTransactionFeeIndex.AvgFeeUsdt =
                ((totalFeeDouble / needUpdateData.DailyAvgTransactionFeeIndex.TransactionCount) *
                 dailyElfPrice).ToString();

            needUpdateData.DailyBlockRewardIndex.TotalBlockCount =
                needUpdateData.EndBlockHeight - needUpdateData.StartBlockHeight + 1;
            needUpdateData.DailyBlockRewardIndex.BlockReward =
                ((double)needUpdateData.TotalReward).ToString("F6");

            needUpdateData.DailyTotalBurntIndex.Burnt = ((double)needUpdateData.TotalBurnt / 1e8).ToString("F6");

            needUpdateData.DailyTransactionCountIndex.BlockCount = totalBlockCount;

            needUpdateData.DailyActiveAddressCountIndex.AddressCount = needUpdateData.AddressSet.Count;
            needUpdateData.DailyActiveAddressCountIndex.SendAddressCount = needUpdateData.AddressFromSet.Count;
            needUpdateData.DailyActiveAddressCountIndex.ReceiveAddressCount = needUpdateData.AddressToSet.Count;

            needUpdateData.DailyTotalContractCallIndex.CallAddressCount = needUpdateData.AddressFromSet.Count;
            needUpdateData.DailyMarketCapIndex.IncrMarketCap =
                (dailyElfPrice * needUpdateData.TotalSupply / 1e8).ToString("F6");
            needUpdateData.DailyMarketCapIndex.FDV =
                (dailyElfPrice * 100000000000000000).ToString("F6");
            needUpdateData.DailyMarketCapIndex.Price = dailyElfPrice.ToString("F6");

            needUpdateData.DailySupplyGrowthIndex.IncrSupply =
                ((double)needUpdateData.TotalSupply / 1e8).ToString("F6");
            needUpdateData.DailySupplyGrowthIndex.Burnt = ((double)needUpdateData.TotalBurnt / 1e8).ToString("F6");
            needUpdateData.DailySupplyGrowthIndex.Reward = ((double)needUpdateData.TotalReward).ToString("F6");

            var dailyContractCallIndexList = needUpdateData.DailyContractCallIndexDic.Values.Select(c =>
            {
                c.CallAddressCount = needUpdateData.CallersDic[c.ContractAddress].Count;
                return c;
            }).ToList();
            var query = await _addressRepository.GetQueryableAsync();
            query = query.Where(c => c.ChainId == chainId);


            var predicates = needUpdateData.AddressSet.Select(s =>
                (Expression<Func<AddressIndex, bool>>)(o => o.Address == s));
            var predicate = predicates.Aggregate((prev, next) => prev.Or(next));
            query = query.Where(predicate);
            var addressList = query.Take(10000).ToList();


            var addressIndices = new List<AddressIndex>();

            if (addressList.IsNullOrEmpty())
            {
                needUpdateData.DailyUniqueAddressCountIndex.AddressCount = needUpdateData.AddressSet.Count;
                foreach (var s in needUpdateData.AddressSet)
                {
                    addressIndices.Add(new AddressIndex()
                    {
                        Date = minDate,
                        Address = s,
                        ChainId = chainId
                    });
                }
            }
            else
            {
                needUpdateData.DailyUniqueAddressCountIndex.AddressCount =
                    needUpdateData.AddressSet.Count - addressList.Count;
                foreach (var addressIndex in addressList)
                {
                    needUpdateData.DailyUniqueAddressCountIndex.AddressCount += addressIndex.Date == minDate ? 1 : 0;
                    if (!needUpdateData.AddressSet.Contains(addressIndex.Address))
                    {
                        addressIndices.Add(new AddressIndex()
                        {
                            Date = minDate,
                            Address = addressIndex.Address,
                            ChainId = chainId
                        });
                    }
                }
            }

            needUpdateData.DailyHasFeeTransactionIndex.TransactionCount =
                needUpdateData.DailyHasFeeTransactionIndex.TransactionIds.Count;

            needUpdateData.DailyStakedIndex.BpStaked = needUpdateData.TotalBpStaked.ToString("F4");
            var ids = new List<string>();
            foreach (var withDrawVotedId in needUpdateData.WithDrawVotedIds)
            {
                if (needUpdateData.DailyVotedIndexDic.TryGetValue(withDrawVotedId, out var v))
                {
                    needUpdateData.TotalVotedStaked -= v.VoteAmount;
                }
                else
                {
                    ids.Add(withDrawVotedId);
                }
            }

            var withDrawVotedAmount = await GetWithDrawVotedAmount(chainId, ids);
            needUpdateData.TotalVotedStaked -= withDrawVotedAmount;
            needUpdateData.DailyStakedIndex.VoteStaked = needUpdateData.TotalVotedStaked.ToString("F4");
            needUpdateData.DailyStakedIndex.Supply = needUpdateData.DailySupplyGrowthIndex.IncrSupply;

            var startNew = Stopwatch.StartNew();
            await _avgTransactionFeeRepository.AddOrUpdateAsync(needUpdateData.DailyAvgTransactionFeeIndex);
            await _blockRewardRepository.AddOrUpdateAsync(needUpdateData.DailyBlockRewardIndex);
            await _totalBurntRepository.AddOrUpdateAsync(needUpdateData.DailyTotalBurntIndex);
            await _deployContractRepository.AddAsync(needUpdateData.DailyDeployContractIndex);
            await _transactionCountRepository.AddOrUpdateAsync(needUpdateData.DailyTransactionCountIndex);
            await _uniqueAddressRepository.AddOrUpdateAsync(needUpdateData.DailyUniqueAddressCountIndex);
            await _activeAddressRepository.AddOrUpdateAsync(needUpdateData.DailyActiveAddressCountIndex);
            await _hasFeeTransactionRepository.AddOrUpdateAsync(needUpdateData.DailyHasFeeTransactionIndex);
            await _dailyTotalContractCallRepository.AddOrUpdateAsync(needUpdateData.DailyTotalContractCallIndex);
            await _dailyMarketCapIndexRepository.AddOrUpdateAsync(needUpdateData.DailyMarketCapIndex);
            await _dailySupplyGrowthIndexRepository.AddOrUpdateAsync(needUpdateData.DailySupplyGrowthIndex);
            await _dailyStakedIndexRepository.AddOrUpdateAsync(needUpdateData.DailyStakedIndex);
            if (!dailyContractCallIndexList.IsNullOrEmpty())
            {
                await _dailyContractCallRepository.AddOrUpdateManyAsync(dailyContractCallIndexList);
            }

            if (!addressIndices.IsNullOrEmpty())
            {
                await _addressRepository.AddOrUpdateManyAsync(addressIndices);
            }

            if (!needUpdateData.DailyVotedIndexDic.IsNullOrEmpty())
            {
                var dailyVotedIndices = needUpdateData.DailyVotedIndexDic.Values.ToList();
                await _dailyVotedIndexRepository.AddOrUpdateManyAsync(dailyVotedIndices);
            }

            await _dailySupplyChangeRepository.AddOrUpdateAsync(needUpdateData.DailySupplyChange);

            startNew.Stop();
            needUpdateData.WirteFinishiTime = DateTime.UtcNow;
            needUpdateData.CostTime = startNew.Elapsed.TotalSeconds;
            dic.Remove(minDate);

            _logger.LogInformation("Update daily transaction data,chainId:{c} date:{d},", chainId, minDate);
            return needUpdateData;
        }


        return null;
    }


    public async Task<double> GetWithDrawVotedAmount(string chainId, List<string> voteIds)
    {
        try
        {
            if (voteIds.IsNullOrEmpty())
            {
                return 0;
            }

            var queryable = await _dailyVotedIndexRepository.GetQueryableAsync();


            var predicates = voteIds.Select(s =>
                (Expression<Func<DailyVotedIndex, bool>>)(o => o.VoteId == s));
            var predicate = predicates.Aggregate((prev, next) => prev.Or(next));
            queryable = queryable.Where(predicate);

            var list = queryable.Where(c => c.ChainId == chainId).Take(1000).ToList();


            if (list.IsNullOrEmpty())
            {
                return 0;
            }


            var sum = list.Sum(c => c.VoteAmount);

            return sum;
        }
        catch (Exception e)
        {
            _logger.LogError("GetWithDrawVotedAmount {chainId},{list},err:{e}", chainId, voteIds, e.Message);
            return 0;
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
                _logger.LogError("TaskERR，UpdateDailyNetwork {c},{e}", chainId, e.Message);
                throw;
            }
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
                _logger.LogError("BatchUpdateNodeNetworkTask err:{c},{e}", chainId, e.Message);
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

    public async Task<double> GetElfPrice(string date)
    {
        try
        {
            var queryableAsync = await _priceRepository.GetQueryableAsync();
            var elfPriceIndices = queryableAsync.Where(c => c.DateStr == date).ToList();

            if (!elfPriceIndices.IsNullOrEmpty())
            {
                var dailyElfPrice = double.Parse(elfPriceIndices[0].Close);
                return dailyElfPrice;
            }


            var res = await _priceServerProvider.GetDailyPriceAsync(new GetDailyPriceRequestDto()
            {
                TokenPair = "elf-usdt",
                TimeStamp = date.Replace("-", "")
            });

            var s = ((double)res.Data.Price / 1e8).ToString();
            _priceRepository.AddOrUpdateAsync(new ElfPriceIndex()
            {
                DateStr = date,
                Close = s
            });

            _logger.LogInformation("GetElfPrice date:{d},price{e}", date, s);
            return (double)res.Data.Price / 1e8;
        }
        catch (Exception e)
        {
            _logger.LogError("GetElfPrice err:{e},date:{d}", e.Message, date.Replace("-", ""));
            return 0;
        }
    }
}