using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.EntityMapping.Repositories;
using AElf.Types;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Dtos.Indexer;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Worker.Core.Dtos;
using AElfScanServer.Worker.Core.Provider;
using Elasticsearch.Net;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using Google.Protobuf;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using IndexerTransactionDto = AElfScanServer.BlockChain.Dtos.IndexerTransactionDto;
using TransactionFeeCharged = AElf.Contracts.MultiToken.TransactionFeeCharged;

namespace AElfScanServer.Worker.Core.Service;

public interface ITransactionService
{
    public Task PullTokenData();
    public Task HandlerTransactionAsync(string chainId, long startBlockHeight, long endBlockHeight);

    public Task UpdateTransactionRatePerMinuteAsync();
    public Task<long> GetLastBlockHeight(string chainId);
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
        IOptionsMonitor<ElasticsearchOptions> options, BlockChainIndexerProvider blockChainIndexerProvider) :
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
    }


    public async Task UpdateTransactionRatePerMinuteAsync()
    {
        await ConnectAsync();

        var chainIds = new List<string>();
        var mergeList = new List<List<TransactionCountPerMinuteDto>>();
        try
        {
            if (_globalOptions.CurrentValue == null)
            {
                _logger.LogError("globalOptions.CurrentValue is null");
                chainIds = new List<string>() { "AELF", "tDVV" };
            }


            if (_globalOptions.CurrentValue.ChainIds.IsNullOrEmpty())
            {
                _logger.LogError("ChainIds is empty");
                chainIds = new List<string>() { "AELF", "tDVV" };
            }

            chainIds = _globalOptions.CurrentValue.ChainIds;


            foreach (var chainId in chainIds)
            {
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


                var transactionChartData =
                    await ParseToTransactionChartDataAsync(chartDataKey, transactionsAsync.Items);

                if (transactionChartData.Count > 180)
                {
                    transactionChartData = transactionChartData.Skip(transactionChartData.Count - 180).ToList();
                }


                mergeList.Add(transactionChartData);
                var serializeObject = JsonConvert.SerializeObject(transactionChartData);


                await RedisDatabase.StringSetAsync(chartDataKey, serializeObject);
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

            _logger.LogError("merge count {c}", merge.Count);

            var mergeSerializeObject = JsonConvert.SerializeObject(merge);
            await RedisDatabase.StringSetAsync(RedisKeyHelper.TransactionChartData("merge"), mergeSerializeObject);
        }
        catch (Exception e)
        {
            _logger.LogError("UpdateTransactionRatePerMinuteAsync error:{e}", e.Message);
            throw e;
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


        var redisValue = RedisDatabase.StringGet(key);
        if (redisValue.IsNullOrEmpty)
        {
            return newList;
        }

        var oldList = JsonConvert.DeserializeObject<List<TransactionCountPerMinuteDto>>(redisValue);

        var last = oldList.Last();
        var subOldList = oldList.GetRange(0, oldList.Count - 1);

        var subNewList = newList.Where(c => c.Start >= last.Start).ToList();

        subOldList.AddRange(subNewList);
        _logger.LogInformation("Merge transaction per minute data chainId:{0},oldList:{1},newList:{2}", key,
            oldList.Count, newList.Count);

        return subOldList;
    }


    public async Task<long> GetLastBlockHeight(string chainId)
    {
        try
        {
            var searchRequest =
                new SearchRequest(BlockChainIndexNameHelper.GenerateTransactionIndexName(chainId))
                {
                    Query = new MatchAllQuery(),
                    Size = 1,
                    Sort = new List<ISort>
                    {
                        new FieldSort() { Field = "blockHeight", Order = SortOrder.Descending },
                    },
                };
            var searchResponse = _elasticClient.Search<TransactionIndex>(searchRequest);
            if (searchResponse.IsValid)
            {
                await HandleIndex(chainId);
                return 0;
            }

            return searchResponse.Documents.First().BlockHeight;
        }
        catch (Exception e)
        {
            await HandleIndex(chainId);

            _logger.LogError("get last block height err:{e}", e);
        }

        return 0;
    }

    public async Task HandleIndex(string chainId)
    {
        if (!_elasticClient.Indices
                .Exists(BlockChainIndexNameHelper.GenerateTransactionIndexName(chainId))
                .Exists)
        {
            var indexResponse = _elasticClient.Indices.Create(
                BlockChainIndexNameHelper.GenerateTransactionIndexName(chainId), c => c
                    .Settings(s => s
                        .Setting("max_result_window", _globalOptions.CurrentValue.TransactionListMaxCount)
                    )
                    .Map<TransactionIndex>(m => m.AutoMap()));
            if (!indexResponse.IsValid)
            {
                throw new Exception($"Failed to index object: {indexResponse.DebugInformation}");
            }
        }
    }


    public void SetAddressIndex(string address, string chainId, List<AddressIndex> addressIndices)
    {
        var addressIndex = _addressIndexRepository
            .GetAsync(address, BlockChainIndexNameHelper.GenerateAddressIndexName(chainId)).Result;

        if (addressIndex != null)
        {
            return;
        }

        addressIndices.Add(new AddressIndex()
        {
            Address = address, IsManager = false,
            AddressType = AddressType.EoaAddress,
            LowerAddress = address.ToLower(),
            Id = address
        });
    }

    public void SetLogEventIndex(string methodName, List<LogEventIndex> logEventIndices,
        IndexerLogEventDto indexerLogEventDto)
    {
        logEventIndices.Add(new LogEventIndex()
        {
            ContractAddress = indexerLogEventDto.ContractAddress,
            BlockHeight = indexerLogEventDto.BlockHeight,
            Index = indexerLogEventDto.Index,
            TransactionId = indexerLogEventDto.TransactionId,
            EventName = indexerLogEventDto.EventName,
            MethodName = methodName
        });
    }


    public void SetBlockExtraIndex(string blockHash, long blockHeight, long burn,
        Dictionary<string, BlockExtraIndex> dictionary)
    {
        if (dictionary.TryGetValue(blockHash, out var value))
        {
            value.BurntFee += burn;
        }
        else
        {
            dictionary[blockHash] = new BlockExtraIndex()
            {
                BurntFee = burn,
                BlockHeight = blockHeight,
                BlockHash = blockHash
            };
        }
    }


    public void AnalysisTransactionLogEvent(string chainId, IndexerTransactionDto txn,
        TransactionIndex transactionIndex, List<AddressIndex> addressIndices, List<TokenInfoIndex> tokenIndices,
        List<LogEventIndex> logEventIndices,
        Dictionary<string, BlockExtraIndex> blockBurnFeeDic)
    {
        double value = 0;
        double fee = 0;
        try
        {
            foreach (var txnLogEvent in txn.LogEvents)
            {
                SetLogEventIndex(transactionIndex.MethodName, logEventIndices, txnLogEvent);

                txnLogEvent.ExtraProperties.TryGetValue("indexed", out var indexed);
                txnLogEvent.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);

                var indexedList = indexed != null
                    ? JsonConvert.DeserializeObject<List<string>>(indexed)
                    : new List<string>();
                var logEvent = new LogEvent
                {
                    Indexed = { indexedList?.Select(ByteString.FromBase64) },
                };

                if (nonIndexed != null)
                {
                    logEvent.NonIndexed = ByteString.FromBase64(nonIndexed);
                }


                switch (txnLogEvent.EventName)
                {
                    case nameof(Transferred):
                        var transferred = new Transferred();
                        transferred.MergeFrom(logEvent);
                        value += transferred.Symbol == "ELF" ? Convert.ToDouble(transferred.Amount) : 0;
                        // if (transferred.From != null)
                        // {
                        //     SetAddressIndex(transferred.From.ToBase58(), chainId, addressIndices);
                        // }
                        //
                        // if (transferred.To != null)
                        // {
                        //     SetAddressIndex(transferred.To.ToBase58(), chainId, addressIndices);
                        // }

                        break;
                    case nameof(TransactionFeeCharged):
                        var transactionFeeCharged = new TransactionFeeCharged();
                        transactionFeeCharged.MergeFrom(logEvent);
                        fee += transactionFeeCharged.Symbol == "ELF"
                            ? Convert.ToDouble(transactionFeeCharged.Amount)
                            : 0;
                        // if (transactionFeeCharged.ChargingAddress != null)
                        // {
                        //     SetAddressIndex(transactionFeeCharged?.ChargingAddress.ToBase58(), chainId, addressIndices);
                        // }

                        break;
                    case nameof(Burned):
                        var burned = new Burned();
                        burned.MergeFrom(logEvent);
                        if (burned.Burner != null)
                        {
                            SetAddressIndex(burned.Burner.ToBase58(), chainId, addressIndices);
                        }

                        SetBlockExtraIndex(transactionIndex.BlockHash, transactionIndex.BlockHeight, burned.Amount,
                            blockBurnFeeDic);
                        break;
                    // case nameof(TokenCreated):
                    //     var tokenCreated = new TokenCreated();
                    //     tokenCreated.MergeFrom(logEvent);
                    //     var token = new TokenInfoIndex
                    //     {
                    //         SymbolType = TokenSymbolHelper.GetSymbolType(tokenCreated.Symbol),
                    //         CollectionSymbol = TokenSymbolHelper.GetCollectionSymbol(tokenCreated.Symbol)
                    //     };
                    //
                    //     if (!tokenCreated.TokenName.IsNullOrWhiteSpace())
                    //     {
                    //         token.LowerTokenName = tokenCreated.TokenName.ToLower();
                    //     }
                    //
                    //     if (!tokenCreated.Symbol.IsNullOrWhiteSpace())
                    //     {
                    //         token.LowerSymbol = tokenCreated.Symbol.ToLower();
                    //     }
                    //
                    //     token.TransactionId = transactionIndex.TransactionId;
                    //     token.BlockHeight = transactionIndex.BlockHeight;
                    //
                    //     _objectMapper.Map(tokenCreated, token);
                    //     tokenIndices.Add(token);
                    //     break;

                    //set manager address
                    // case nameof(CAHolderCreated):
                    //     var cAHolderCreated = new CAHolderCreated();
                    //     cAHolderCreated.MergeFrom(logEvent);
                    //     if (cAHolderCreated.Manager != null)
                    //     {
                    //         SetManagerAddressIndex(cAHolderCreated.Manager.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                    // case nameof(ManagerInfoSocialRecovered):
                    //     var managerInfoSocialRecovered = new ManagerInfoSocialRecovered();
                    //     managerInfoSocialRecovered.MergeFrom(logEvent);
                    //     if (managerInfoSocialRecovered.Manager != null)
                    //     {
                    //         SetManagerAddressIndex(managerInfoSocialRecovered.Manager.ToBase58(), chainId,
                    //             addressIndices);
                    //     }
                    //
                    //     break;
                    // case nameof(ManagerInfoAdded):
                    //     var managerInfoAdded = new ManagerInfoAdded();
                    //     managerInfoAdded.MergeFrom(logEvent);
                    //     if (managerInfoAdded.Manager != null)
                    //     {
                    //         SetManagerAddressIndex(managerInfoAdded.Manager.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;

                    // case nameof(CrossChainReceived):
                    //     var crossChainReceivecd = new CrossChainReceived();
                    //     crossChainReceivecd.MergeFrom(logEvent);
                    //     if (crossChainReceivecd.To != null)
                    //     {
                    //         SetAddressIndex(crossChainReceivecd.To.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                    // case nameof(CrossChainTransferred):
                    //     var crossChainTransferred = new CrossChainTransferred();
                    //     crossChainTransferred.MergeFrom(logEvent);
                    //     if (crossChainTransferred.From != null)
                    //     {
                    //         SetAddressIndex(crossChainTransferred.From.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     if (crossChainTransferred.To != null)
                    //     {
                    //         SetAddressIndex(crossChainTransferred.To.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                    //
                    // case nameof(Issued):
                    //     var issued = new Issued();
                    //     issued.MergeFrom(logEvent);
                    //     if (issued.To != null)
                    //     {
                    //         SetAddressIndex(issued.To.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;

                    // case nameof(RentalCharged):
                    //     var rentalCharged = new RentalCharged();
                    //     rentalCharged.MergeFrom(logEvent);
                    //     if (rentalCharged.Payer != null)
                    //     {
                    //         SetAddressIndex(rentalCharged.Payer.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                    //
                    // case nameof(ResourceTokenClaimed):
                    //     var resourceTokenClaimed = new ResourceTokenClaimed();
                    //     resourceTokenClaimed.MergeFrom(logEvent);
                    //     if (resourceTokenClaimed.Payer != null)
                    //     {
                    //         SetAddressIndex(resourceTokenClaimed.Payer.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;

                    // case nameof(TransactionFeeClaimed):
                    //     var transactionFeeClaimed = new TransactionFeeClaimed();
                    //     transactionFeeClaimed.MergeFrom(logEvent);
                    //     if (transactionFeeClaimed.Receiver != null)
                    //     {
                    //         SetAddressIndex(transactionFeeClaimed.Receiver.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                    //
                    // case nameof(ContractDeployed):
                    //     var contractDeployed = new ContractDeployed();
                    //     contractDeployed.MergeFrom(logEvent);
                    //     if (contractDeployed.Address != null)
                    //     {
                    //         SetContractAddressIndex(contractDeployed.Address.ToBase58(), chainId, addressIndices);
                    //     }
                    //
                    //     break;
                }
            }

            transactionIndex.Value = value.ToString();
            transactionIndex.TxnFee = fee.ToString();
        }
        catch (Exception e)
        {
            _logger.LogError("AnalysisTransactionLogEvent  error:{e}.txId:{id},blockHeight:{h}", e.Message,
                txn.TransactionId,
                txn.BlockHeight);
        }
    }


    public async Task SetTransactionPerSecond(Dictionary<string, int> transactionRate, string chainId)
    {
        // foreach (var keyValuePair in transactionRate)
        // {
        //     await ConnectAsync();
        //     RedisDatabase.SortedSetIncrement(RedisKeyHelper.TransactionTPS(chainId), keyValuePair.Key,
        //         keyValuePair.Value);
        //     // await RedisDatabase.KeyExpireAsync(keyValuePair.Key,
        //     //     TimeSpan.FromSeconds(_aelfIndexerOptions.TransactionRateKeyExpireDurationSeconds));
        // }
    }


    public async Task HandlerTransactionAsync(string chainId, long startBlockHeight, long endBlockHeight)
    {
        await ConnectAsync();
        var indexerTransactionDtos = new List<IndexerTransactionDto>();
        for (long i = startBlockHeight; i <= endBlockHeight; i += 100)
        {
            var start = i;
            var end = i + 100 > endBlockHeight ? endBlockHeight : i + 100;

            indexerTransactionDtos.AddRange(_aelfIndexerProvider.GetTransactionsAsync(
                chainId,
                start,
                end).Result);
        }


        var transactionIndices = new List<TransactionIndex>();
        var addressIndices = new List<AddressIndex>();
        var tokenInfoIndices = new List<TokenInfoIndex>();
        var logEventIndices = new List<LogEventIndex>();
        var blockBurnFeeDic = new Dictionary<string, BlockExtraIndex>();
        var transactionRate = new Dictionary<string, int>();


        var hashSet = new HashSet<string>();

        foreach (var transactionDto in indexerTransactionDtos)
        {
            if (hashSet.Contains(transactionDto.TransactionId))
            {
                continue;
            }

            hashSet.Add(transactionDto.TransactionId);
            var transaction = _objectMapper.Map<IndexerTransactionDto, TransactionIndex>(transactionDto);
            AnalysisTransactionLogEvent(chainId, transactionDto, transaction, addressIndices,
                tokenInfoIndices, logEventIndices, blockBurnFeeDic);
            // var t = (long)transaction.BlockTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds / 10;
            // var key =
            //     Convert.ToString(t / 10);
            // if (transactionRate.ContainsKey(key))
            // {
            //     transactionRate[key]++;
            // }
            // else
            // {
            //     transactionRate.Add(key, 1);
            // }
            //
            transactionIndices.Add(transaction);
        }

        if (transactionIndices.Count == 0)
        {
            _logger.LogInformation("transactionList is empty,chainId:{chainId}", chainId);
            return;
        }


        // await SetTransactionPerSecond(transactionRate, chainId);


        // if (!addressIndices.IsNullOrEmpty())
        // {
        //     await _addressIndexRepository.AddOrUpdateManyAsync(addressIndices,
        //         BlockChainIndexNameHelper.GenerateAddressIndexName(chainId));
        // }
        //
        // if (!blockBurnFeeDic.IsNullOrEmpty())
        // {
        //     var blockExtraIndices = blockBurnFeeDic.Values.ToList();
        //
        //     await _blockExtraIndexRepository.AddOrUpdateManyAsync(blockExtraIndices,
        //         BlockChainIndexNameHelper.GenerateBlockExtraIndexName(chainId));
        // }

        //
        //
        // if (!logEventIndices.IsNullOrEmpty())
        // {
        //     await _logEventIndexRepository.AddOrUpdateManyAsync(logEventIndices,
        //         BlockChainIndexNameHelper.GenerateLogEventIndexName(chainId));
        // }
        //
        // if (!tokenInfoIndices.IsNullOrEmpty())
        // {
        //     await _tokenInfoIndexRepository.AddOrUpdateManyAsync(tokenInfoIndices,
        //         BlockChainIndexNameHelper.GenerateTokenIndexName(chainId));
        // }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _logger.LogInformation("start add or update transaction list,chainId:{0},count:{1},blockRange:[{2},{3}]",
            chainId,
            transactionIndices.Count, startBlockHeight, endBlockHeight);
        var bulkResponse = _elasticClient.Bulk(b =>
            b.Index(BlockChainIndexNameHelper.GenerateTransactionIndexName(chainId)).IndexMany(transactionIndices));
        if (!bulkResponse.IsValid)
        {
            _logger.LogError("bulk transaction error:{e}", bulkResponse.ServerError.Error.Reason);
            return;
        }

        //
        // await _transactionIndexRepository.AddOrUpdateManyAsync(transactionIndices,
        //     BlockChainIndexNameHelper.GenerateTransactionIndexName(chainId));
        stopwatch.Stop();
        _logger.LogInformation(
            "Success! add or update transaction list,chainId:{0},count:{1},blockRange:[{2},{3}],cost time:{4}", chainId,
            transactionIndices.Count, startBlockHeight, endBlockHeight, stopwatch.Elapsed.TotalSeconds);
    }

    public async Task PullTokenData()
    {
        var settings = new ConnectionSettings(new Uri("http://192.168.67.208:9200"))
            .DefaultIndex("aelfindexer.transactionindex");

        var client = new ElasticClient(settings);

        var response = client.Search<SearchResult>(s => s
            .Source(src => src.Includes(f => f.Field("blockHeight")))
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                            .Nested(n => n
                                .Path("LogEvents")
                                .Query(nq => nq
                                    .Bool(nb => nb
                                        .Must(mq => mq
                                            .Match(mq => mq
                                                .Field("LogEvents.eventName")
                                                .Query("Transferred")
                                            )
                                        )
                                    )
                                )
                            ),
                        m => m.Term(t => t
                            .Field("chainId")
                            .Value("AELF")
                        )
                    )
                )
            )
            .Aggregations(a => a
                .Terms("field1_list", t => t
                    .Field("blockHeight")
                    .Size(1000)
                )
            )
        );

        if (response.IsValid)
        {
            var aggregations = response.Aggregations;
            if (aggregations != null && aggregations.TryGetValue("field1_list", out var field1ListAggregation))
            {
                var field1List = field1ListAggregation as BucketAggregate;
                if (field1List != null)
                {
                    var buckets = field1List.Items;
                    foreach (var bucket in buckets)
                    {
                        var keyedBucket = (KeyedBucket<object>)bucket;
                        var keyedBucketKey = keyedBucket.Key.ToString();
                        await HandlerTransactionAsync("AELF", Convert.ToInt32(keyedBucketKey),
                            Convert.ToInt32(keyedBucketKey));
                    }
                }
            }
        }
    }
}