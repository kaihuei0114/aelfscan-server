using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Indexing.Elasticsearch;
using AElf.Types;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using Elasticsearch.Net;
using AElfScanServer.Dtos;
using AElfScanServer.Helper;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElfScanServer.BlockChain.Dtos.Indexer;
using Castle.Components.DictionaryAdapter.Xml;
using AElfScanServer.Common.Helper;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using StackExchange.Redis;
using Volo.Abp.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Nito.AsyncEx;
using IndexerTransactionDto = AElfScanServer.BlockChain.Dtos.IndexerTransactionDto;
using TransactionStatus = AElfScanServer.BlockChain.Dtos.TransactionStatus;

namespace AElfScanServer.BlockChain.HttpApi.Service;

public interface IBlockChainService

{
    public Task<TransactionsResponseDto> GetTransactionsAsync(TransactionsRequestDto requestD);


    public Task<BlocksResponseDto> GetBlocksAsync(BlocksRequestDto requestDto);

    public Task<BlockDetailResponseDto> GetBlockDetailAsync(BlockDetailRequestDto requestDto);


    public Task<TransactionDetailResponseDto> GetTransactionDetailAsync(TransactionDetailRequestDto request);

    public Task<LogEventResponseDto> GetLogEventsAsync(GetLogEventRequestDto request);
}

public class BlockChainService : IBlockChainService, ITransientDependency
{
    private readonly INESTRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly INESTRepository<BlockExtraIndex, string> _blockExtraIndexRepository;
    private readonly INESTRepository<AddressIndex, string> _addressIndexRepository;
    private readonly INESTRepository<TokenInfoIndex, string> _tokenInfoIndexRepository;
    private readonly GlobalOptions _globalOptions;
    private readonly IElasticClient _elasticClient;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;
    private readonly BlockChainDataProvider _blockChainProvider;
    private readonly LogEventProvider _logEventProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IAddressService _addressService;

    private readonly ILogger<HomePageService> _logger;
    private const long PullBlockHeightInterval = 100;


    public BlockChainService(INESTRepository<TransactionIndex, string> transactionIndexRepository,
        ILogger<HomePageService> logger, IOptionsMonitor<GlobalOptions> blockChainOptions,
        AELFIndexerProvider aelfIndexerProvider, IOptions<ElasticsearchOptions> options,
        INESTRepository<BlockExtraIndex, string> blockExtraIndexRepository,
        INESTRepository<AddressIndex, string> addressIndexRepository,
        INESTRepository<TokenInfoIndex, string> tokenInfoIndexRepository,
        LogEventProvider logEventProvider, IObjectMapper objectMapper,
        IAddressService addressService,
        BlockChainDataProvider blockChainProvider, IBlockChainIndexerProvider blockChainIndexerProvider)
    {
        _transactionIndexRepository = transactionIndexRepository;
        _logger = logger;
        _globalOptions = blockChainOptions.CurrentValue;
        _aelfIndexerProvider = aelfIndexerProvider;
        var uris = options.Value.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
        _blockExtraIndexRepository = blockExtraIndexRepository;
        _addressIndexRepository = addressIndexRepository;
        _tokenInfoIndexRepository = tokenInfoIndexRepository;
        _logEventProvider = logEventProvider;
        _objectMapper = objectMapper;
        _addressService = addressService;
        _blockChainProvider = blockChainProvider;
        _blockChainIndexerProvider = blockChainIndexerProvider;
    }


    public async Task<TransactionDetailResponseDto> GetTransactionDetailAsync(TransactionDetailRequestDto request)
    {
        var transactionDetailResponseDto = new TransactionDetailResponseDto();
        if (!_globalOptions.ChainIds.Exists(s => s == request.ChainId))
        {
            return transactionDetailResponseDto;
        }

        try
        {
            // GetTokenImage();
            var transactionsAsync =
                await _aelfIndexerProvider.GetTransactionsAsync(request.ChainId,
                    request.BlockHeight == 0 ? 0 : request.BlockHeight,
                    request.BlockHeight);

            var aElfClient = new AElfClient(_globalOptions.ChainNodeHosts[request.ChainId]);

            var blockHeightAsync = await aElfClient.GetBlockHeightAsync();
            for (var i = 0; i < transactionsAsync.Count; i++)
            {
                if (transactionsAsync[i].TransactionId == request.TransactionId)
                {
                    transactionDetailResponseDto.List.Add(await AnalysisTransaction(transactionsAsync[i],
                        blockHeightAsync));


                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTransactionDetailAsync error ");
        }


        return transactionDetailResponseDto;
    }


    public async void GetTokenImage()
    {
        var tokenImageBlockHeight = await ParseTokenImageBlockHeight();


        var dictionary = new Dictionary<string, string>();

        foreach (var height in tokenImageBlockHeight)
        {
            var logEventAsync = await _aelfIndexerProvider.GetLogEventAsync("AELF", height, height);
            foreach (var indexerLogEventDto in logEventAsync)
            {
                if (indexerLogEventDto.EventName == "TokenCreated")
                {
                    indexerLogEventDto.ExtraProperties.TryGetValue("Indexed", out var indexed);
                    indexerLogEventDto.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);

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

                    var tokenCreated = new TokenCreated();
                    tokenCreated.MergeFrom(logEvent);

                    if (TokenSymbolHelper.GetSymbolType(tokenCreated.Symbol) == SymbolType.Nft)
                    {
                        if (indexerLogEventDto.ExtraProperties.TryGetValue(CommomHelper.GetNftImageKey(),
                                out var nftImageUrl))
                        {
                            dictionary[tokenCreated.Symbol] = nftImageUrl;
                        }
                    }
                }
            }
        }
    }


    public async Task<List<long>> ParseTokenImageBlockHeight()
    {
        string[] lines = File.ReadAllLines("result.txt");

        // 解析每一行为long类型，并存储到数组中
        long[] numbers = new long[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            if (long.TryParse(lines[i], out long number))
            {
                numbers[i] = number;
            }
            else
            {
                Console.WriteLine($"Error parsing line {i + 1}: {lines[i]}");
            }
        }

        var result = new List<long>() { numbers[0] };

        for (var i = 1; i < numbers.Length; i++)
        {
            if (numbers[i] - result[result.Count - 1] > 100)
            {
                result.Add(numbers[i]);
            }
        }

        return numbers.ToList();
    }

    public async Task<BlockDetailResponseDto> GetBlockDetailAsync(BlockDetailRequestDto requestDto)
    {
        var blockResponseDto = new BlockDetailResponseDto();


        List<Task> blockDetailsTasks = new List<Task>();


        var blockDto = new BlockDetailDto();

        var transactionList = new List<IndexerTransactionDto>();

        var blockList = new List<IndexerBlockDto>();

        var blockDtoTask =
            _blockChainProvider.GetBlockDetailAsync(requestDto.ChainId, requestDto.BlockHeight).ContinueWith(task =>
            {
                blockDto = task.Result;
            });


        var transactionListTask =
            _aelfIndexerProvider.GetTransactionsAsync(requestDto.ChainId, requestDto.BlockHeight,
                requestDto.BlockHeight).ContinueWith(task => { transactionList = task.Result; });

        var blockListTask = _aelfIndexerProvider.GetLatestBlocksAsync(requestDto.ChainId, requestDto.BlockHeight - 1,
            requestDto.BlockHeight + 1).ContinueWith(task => { blockList = task.Result; });

        blockDetailsTasks.Add(blockDtoTask);
        blockDetailsTasks.Add(transactionListTask);
        blockDetailsTasks.Add(blockListTask);

        await blockDetailsTasks.WhenAll();


        blockResponseDto.BlockHeight = requestDto.BlockHeight;
        blockResponseDto.ChainId = requestDto.ChainId;
        var fee = await _blockChainProvider.GetBlockRewardAsync(requestDto.BlockHeight, requestDto.ChainId);
        blockResponseDto.BurntFee = new BurntFee()
        {
            ElfFee = fee,

            UsdFee =
                (double.Parse(await _blockChainProvider.GetTokenUsdPriceAsync("ELF")) * double.Parse(fee)).ToString()
        };

        var reward = await _blockChainProvider.GetBlockRewardAsync(requestDto.BlockHeight, requestDto.ChainId);
        blockResponseDto.Reward = new RewardDto()
        {
            ElfReward = reward,
            UsdReward = (double.Parse(await _blockChainProvider.GetTokenUsdPriceAsync("ELF")) * double.Parse(reward))
                .ToString()
        };

        var midBlockDto = blockList.Where(b => b.BlockHeight == requestDto.BlockHeight).First();

        blockResponseDto.Confirmed = midBlockDto.Confirmed;
        blockResponseDto.Timestamp = DateTimeHelper.GetTotalSeconds(midBlockDto.BlockTime);
        blockResponseDto.BlockHash = midBlockDto.BlockHash;
        blockResponseDto.Producer = new Producer()
        {
            address = midBlockDto.Miner,
            name = await GetBpNameAsync(midBlockDto.ChainId, midBlockDto.Miner)
        };


        blockResponseDto.PreviousBlockHash = blockDto.Header.PreviousBlockHash;
        blockResponseDto.BlockSize = blockDto.BlockSize.ToString();

        blockResponseDto.PreviousBlockHash = blockDto.Header.PreviousBlockHash;

        blockResponseDto.PreviousBlockHash = blockDto.Header.PreviousBlockHash;
        blockResponseDto.MerkleTreeRootOfTransactions = blockDto.Header.MerkleTreeRootOfTransactions;
        blockResponseDto.MerkleTreeRootOfWorldState = blockDto.Header.MerkleTreeRootOfWorldState;
        blockResponseDto.MerkleTreeRootOfTransactionState = blockDto.Header.MerkleTreeRootOfTransactionState;
        blockResponseDto.Extra = blockDto.Header.Extra;
        blockResponseDto.Transactions = new List<TransactionResponseDto>();
        blockResponseDto.PreBlockHeight = blockList.Exists(b => b.BlockHeight == requestDto.BlockHeight - 1)
            ? requestDto.BlockHeight - 1
            : 0;

        blockResponseDto.NextBlockHeight = blockList.Exists(b => b.BlockHeight == requestDto.BlockHeight + 1)
            ? requestDto.BlockHeight + 1
            : 0;
        foreach (var transactionIndex in transactionList)
        {
            var transactionRespDto = new TransactionResponseDto()
            {
                TransactionId = transactionIndex.TransactionId,
                Timestamp = DateTimeHelper.GetTotalSeconds(transactionIndex.BlockTime),
                BlockHeight = transactionIndex.BlockHeight,
                Method = transactionIndex.MethodName,
                Status = transactionIndex.Status
            };

            transactionRespDto.From = ConvertAddress(transactionIndex.From, transactionIndex.ChainId);

            transactionRespDto.To = ConvertAddress(transactionIndex.To, transactionIndex.ChainId);

            var value = await ParseIndexerTransactionValueInfoAsync(transactionIndex);
            transactionRespDto.TransactionValue = value.Item1;

            transactionRespDto.TransactionFee = value.Item2;
            blockResponseDto.Transactions.Add(transactionRespDto);
        }

        blockResponseDto.Total = blockResponseDto.Transactions.Count;


        return blockResponseDto;
    }


    public async Task<TransactionDetailDto> AnalysisTransaction(IndexerTransactionDto transactionDto, long blockHeight)
    {
        var detailDto = new TransactionDetailDto();


        detailDto.TransactionId = transactionDto.TransactionId;
        detailDto.Status = transactionDto.Status;
        detailDto.BlockConfirmations = detailDto.Status == TransactionStatus.Mined ? blockHeight : 0;
        detailDto.BlockHeight = transactionDto.BlockHeight;
        detailDto.Timestamp = DateTimeHelper.GetTotalSeconds(transactionDto.BlockTime);
        detailDto.Method = transactionDto.MethodName;
        detailDto.TransactionParams = transactionDto.Params;
        detailDto.TransactionSignature = transactionDto.Signature;
        detailDto.Confirmed = transactionDto.Confirmed;
        detailDto.From = ConvertAddress(transactionDto.From, transactionDto.ChainId);
        detailDto.To = ConvertAddress(transactionDto.To, transactionDto.ChainId);


        await AnalysisExtraPropertiesAsync(detailDto, transactionDto);
        await AnalysisTransferredAsync(detailDto, transactionDto);
        await AnalysisLogEventAsync(detailDto, transactionDto);

        return detailDto;
    }


    public async Task AnalysisLogEventAsync(TransactionDetailDto detailDto, IndexerTransactionDto transactionDto)
    {
        foreach (var transactionDtoLogEvent in transactionDto.LogEvents)
        {
            transactionDtoLogEvent.ExtraProperties.TryGetValue("Indexed", out var indexed);
            transactionDtoLogEvent.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);
            var logEventInfoDto = new LogEventInfoDto()
            {
                Indexed = indexed,
                NonIndexed = nonIndexed,
                EventName = transactionDtoLogEvent.EventName,
                ContractInfo = ConvertAddress(transactionDtoLogEvent.ContractAddress, transactionDto.ChainId)
            };
            detailDto.LogEvents.Add(logEventInfoDto);
            //add parse log event logic
            if (!indexed.IsNullOrEmpty() &&
                (_globalOptions.ParseLogEvent(detailDto.From.Address, detailDto.Method)
                 || _globalOptions.ParseLogEvent(detailDto.To.Address, detailDto.Method)))
            {
                var message = ParseMessage(transactionDtoLogEvent.EventName, ByteString.FromBase64(indexed));
                detailDto.AddParseLogEvents(message);
            }
        }
    }

    public async Task AnalysisExtraPropertiesAsync(TransactionDetailDto detailDto, IndexerTransactionDto transactionDto)
    {
        if (!transactionDto.ExtraProperties.IsNullOrEmpty())
        {
            if (transactionDto.ExtraProperties.TryGetValue("Version", out var version))
            {
                detailDto.Version = version;
            }


            if (transactionDto.ExtraProperties.TryGetValue("RefBlockNumber", out var refBlockNumber))
            {
                detailDto.TransactionRefBlockNumber = refBlockNumber;
            }

            if (transactionDto.ExtraProperties.TryGetValue("RefBlockPrefix", out var refBlockPrefix))
            {
                detailDto.TransactionRefBlockPrefix = refBlockPrefix;
            }


            if (transactionDto.ExtraProperties.TryGetValue("Bloom", out var bloom))
            {
                detailDto.Bloom = bloom;
            }


            if (transactionDto.ExtraProperties.TryGetValue("ReturnValue", out var returnValue))
            {
                detailDto.ReturnValue = returnValue;
            }


            if (transactionDto.ExtraProperties.TryGetValue("Error", out var error))
            {
                detailDto.Error = error;
            }


            if (transactionDto.ExtraProperties.TryGetValue("TransactionSize", out var transactionSize))
            {
                detailDto.TransactionSize = transactionSize;
            }


            if (transactionDto.ExtraProperties.TryGetValue("ResourceFee", out var resourceFee))
            {
                detailDto.ResourceFee = resourceFee;
            }
        }
    }


    public async Task AnalysisTransferredAsync(TransactionDetailDto detailDto,
        IndexerTransactionDto indexerTransactionDto)
    {
        var transactionValues = new Dictionary<string, ValueInfoDto>();

        var transactionFees = new Dictionary<string, ValueInfoDto>();


        var burntFees = new Dictionary<string, ValueInfoDto>();


        foreach (var txnLogEvent in indexerTransactionDto.LogEvents)
        {
            txnLogEvent.ExtraProperties.TryGetValue("Indexed", out var indexed);
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

                    await SetValueInfoAsync(transactionValues, transferred.Symbol, transferred.Amount);
                    if (transactionValues.TryGetValue(transferred.Symbol, out var value))
                    {
                        value.Amount += transferred.Amount;
                    }
                    else
                    {
                        transactionValues.Add(transferred.Symbol, new ValueInfoDto()
                        {
                            Amount = transferred.Amount,
                            Symbol = transferred.Symbol,
                        });
                    }


                    if (TokenSymbolHelper.GetSymbolType(transferred.Symbol) == SymbolType.Token)
                    {
                        _globalOptions.TokenImageUrls.TryGetValue(transferred.Symbol, out var imageUrl);
                        var token = new TokenTransferredDto()
                        {
                            Symbol = transferred.Symbol,
                            Name = transferred.Symbol,
                            Amount = transferred.Amount,
                            From = ConvertAddress(transferred.From.ToBase58(), indexerTransactionDto.ChainId),
                            To = ConvertAddress(transferred.To.ToBase58(), indexerTransactionDto.ChainId),
                            ImageBase64 = await _blockChainProvider.GetTokenImageBase64Async(transferred.Symbol),
                            NowPrice = await _blockChainProvider.GetTokenUsdPriceAsync(transferred.Symbol)
                        };
                        detailDto.TokenTransferreds.Add(token);
                    }
                    else
                    {
                        var nft = new NftsTransferredDto()
                        {
                            Symbol = transferred.Symbol,
                            Amount = transferred.Amount,
                            Name = transferred.Symbol,
                            From = ConvertAddress(transferred.From.ToBase58(), indexerTransactionDto.ChainId),
                            To = ConvertAddress(transferred.To.ToBase58(), indexerTransactionDto.ChainId),
                            IsCollection = TokenSymbolHelper.IsCollection(transferred.Symbol),
                            ImageBase64 = await _blockChainProvider.GetTokenImageBase64Async(transferred.Symbol),
                        };
                        detailDto.NftsTransferreds.Add(nft);
                    }

                    break;
                case nameof(TransactionFeeCharged):
                    var transactionFeeCharged = new TransactionFeeCharged();
                    transactionFeeCharged.MergeFrom(logEvent);
                    await SetValueInfoAsync(transactionFees, transactionFeeCharged.Symbol,
                        transactionFeeCharged.Amount);

                    break;
                case nameof(Burned):
                    var burned = new Burned();
                    burned.MergeFrom(logEvent);
                    await SetValueInfoAsync(burntFees, burned.Symbol, burned.Amount);
                    break;
            }
        }


        foreach (var valueInfoDto in transactionValues)
        {
            valueInfoDto.Value.NowPrice = await _blockChainProvider.GetTokenUsdPriceAsync(valueInfoDto.Value.Symbol);
        }

        foreach (var valueInfoDto in transactionFees)
        {
            valueInfoDto.Value.NowPrice = await _blockChainProvider.GetTokenUsdPriceAsync(valueInfoDto.Value.Symbol);
        }


        foreach (var valueInfoDto in burntFees)
        {
            valueInfoDto.Value.NowPrice = await _blockChainProvider.GetTokenUsdPriceAsync(valueInfoDto.Value.Symbol);
        }


        detailDto.TransactionFees = transactionFees.Values.OrderByDescending(x => x.Amount).ToList();

        detailDto.TransactionValues = transactionValues.Values.OrderByDescending(x => x.Amount).ToList();
        detailDto.BurntFees = burntFees.Values.OrderByDescending(x => x.Amount).ToList();
    }


    public async Task SetValueInfoAsync(Dictionary<string, ValueInfoDto> dic, string symbol, long amount)
    {
        if (dic.TryGetValue(symbol, out var value))
        {
            value.Amount += amount;
        }
        else
        {
            dic.Add(symbol, new ValueInfoDto()
            {
                Amount = amount,
                Symbol = symbol
            });
        }
    }


    public async Task<LogEventResponseDto> GetLogEventsAsync(GetLogEventRequestDto request)
    {
        return await _logEventProvider.GetLogEventListAsync(request);
    }


    public async Task<BlocksResponseDto> GetBlocksAsync(BlocksRequestDto requestDto)
    {
        var result = new BlocksResponseDto() { };

        try
        {
            Stopwatch stopwatch1 = new Stopwatch();
            stopwatch1.Start();
            var summariesList = await _aelfIndexerProvider.GetLatestSummariesAsync(requestDto.ChainId);

            var blockHeightAsync = summariesList.First().LatestBlockHeight;
            stopwatch1.Stop();

            var endBlockHeight = blockHeightAsync - requestDto.SkipCount;
            var startBlockHeight = endBlockHeight - requestDto.MaxResultCount;

            _logger.LogInformation($"time get blockheight:{stopwatch1.Elapsed.TotalSeconds}");

            List<Task> getBlockRawDataTasks = new List<Task>();
            List<IndexerBlockDto> blockList = new List<IndexerBlockDto>();
            Dictionary<string, long> blockBurntFee = new Dictionary<string, long>();

            Stopwatch stopwatch2 = new Stopwatch();
            stopwatch2.Start();

            var blockListTask = _aelfIndexerProvider.GetLatestBlocksAsync(requestDto.ChainId,
                startBlockHeight,
                endBlockHeight).ContinueWith(task => { blockList = task.Result; });

            getBlockRawDataTasks.Add(blockListTask);

            var blockBurntFeeTask = ParseBlockBurntAsync(requestDto.ChainId,
                startBlockHeight,
                endBlockHeight).ContinueWith(task => { blockBurntFee = task.Result; });

            getBlockRawDataTasks.Add(blockBurntFeeTask);

            await Task.WhenAll(getBlockRawDataTasks);

            stopwatch2.Stop();
            _logger.LogInformation($"time get raw data:{stopwatch2.Elapsed.TotalSeconds}");

            result.Blocks = new List<BlockResponseDto>();
            result.Total = blockHeightAsync;


            // List<Task> tasks = new List<Task>();

            Stopwatch stopwatch3 = new Stopwatch();
            stopwatch3.Start();


            for (var i = blockList.Count - 1; i > 0; i--)
            {
                var indexerBlockDto = blockList[i];
                var latestBlockDto = new BlockResponseDto();

                latestBlockDto.BlockHeight = indexerBlockDto.BlockHeight;
                latestBlockDto.Timestamp = DateTimeHelper.GetTotalSeconds(indexerBlockDto.BlockTime);
                latestBlockDto.TransactionCount = indexerBlockDto.TransactionIds.Count;
                latestBlockDto.ProducerAddress = indexerBlockDto.Miner;
                latestBlockDto.ProducerName = await GetBpNameAsync(indexerBlockDto.ChainId, indexerBlockDto.Miner);


                latestBlockDto.BurntFees = blockBurntFee.TryGetValue(indexerBlockDto.BlockHash, out var value)
                    ? value.ToString()
                    : "0";

                latestBlockDto.TimeSpan = (Convert.ToDouble(0 < blockList.Count
                    ? DateTimeHelper.GetTotalMilliseconds(indexerBlockDto.BlockTime) -
                      DateTimeHelper.GetTotalMilliseconds(blockList[i - 1].BlockTime)
                    : 0) / 1000).ToString("0.0");

                result.Blocks.Add(latestBlockDto);
                latestBlockDto.Reward = "12500000";
            }


            stopwatch3.Stop();
            _logger.LogInformation($"time parse data:{stopwatch3.Elapsed.TotalSeconds}");
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetLatestBlocksAsync error");
        }

        return result;
    }


    public async Task<string> GetBpNameAsync(string chainId, string address)
    {
        if (_globalOptions.BPNames.TryGetValue(chainId, out var bpNames))
        {
            if (bpNames.TryGetValue(address, out var name))
            {
                return name;
            }
        }

        return address;
    }

    public async Task<Dictionary<string, long>> ParseBlockBurntAsync(string chainId, long startBlockHeight,
        long endBlockHeight)
    {
        var result = new Dictionary<string, long>();
        try
        {
            var logList = await _aelfIndexerProvider.GetLogEventAsync(chainId,
                startBlockHeight,
                endBlockHeight);

            foreach (var indexerLogEventDto in logList)
            {
                if (indexerLogEventDto.EventName != nameof(Burned))
                {
                    continue;
                }

                indexerLogEventDto.ExtraProperties.TryGetValue("Indexed", out var indexed);
                indexerLogEventDto.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);
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

                var burned = new Burned();
                burned.MergeFrom(logEvent);
                if (burned.Symbol != "ELF" && burned.Symbol != "")
                {
                    continue;
                }

                if (result.ContainsKey(indexerLogEventDto.BlockHash))
                {
                    result[indexerLogEventDto.BlockHash] += burned.Amount;
                }
                else
                {
                    result[indexerLogEventDto.BlockHash] = burned.Amount;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"ParseBlockBurntAsync error:{e}");
        }


        return result;
    }

    public async Task<TransactionsResponseDto> GetTransactionsAsync(TransactionsRequestDto requestDto)
    {
        var result = new TransactionsResponseDto();
        result.Transactions = new List<TransactionResponseDto>();

        try
        {
            var indexerTransactionList = await _blockChainIndexerProvider.GetTransactionsAsync(requestDto.ChainId,
                requestDto.SkipCount, requestDto.MaxResultCount, 0, 0, requestDto.Address);


            foreach (var transactionIndex in indexerTransactionList.Items)
            {
                var transactionRespDto = new TransactionResponseDto()
                {
                    TransactionId = transactionIndex.TransactionId,
                    Timestamp = DateTimeHelper.GetTotalSeconds(transactionIndex.Metadata.Block.BlockTime),
                    TransactionValue = transactionIndex.TransactionValue.ToString(),
                    BlockHeight = transactionIndex.BlockHeight,
                    Method = transactionIndex.MethodName,
                    Status = transactionIndex.Status,
                    TransactionFee = transactionIndex.Fee.ToString(),
                };


                transactionRespDto.From = ConvertAddress(transactionIndex.From, requestDto.ChainId);

                transactionRespDto.To = ConvertAddress(transactionIndex.To, requestDto.ChainId);
                result.Transactions.Add(transactionRespDto);
            }

            result.Total = indexerTransactionList.TotalCount;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetLatestTransactionsAsync error");
            return result;
        }

        return result;
    }


    public async Task<TransactionsResponseDto> ParseIndexerTransactionListAsync(List<IndexerTransactionDto> list)
    {
        var transactionsResponseDto = new TransactionsResponseDto();
        foreach (var indexerTransactionDto in list)
        {
            var transactionResponseDto = new TransactionResponseDto()
            {
                TransactionId = indexerTransactionDto.TransactionId,
                BlockHeight = indexerTransactionDto.BlockHeight,
                Method = indexerTransactionDto.MethodName,
                Status = indexerTransactionDto.Status,
                From = ConvertAddress(indexerTransactionDto.From, indexerTransactionDto.ChainId),
                To = ConvertAddress(indexerTransactionDto.To, indexerTransactionDto.ChainId),
                Timestamp = DateTimeHelper.GetTotalSeconds(indexerTransactionDto.BlockTime),
            };
            var value = await ParseIndexerTransactionValueInfoAsync(indexerTransactionDto);

            transactionResponseDto.TransactionValue = value.Item1;
            transactionResponseDto.TransactionFee = value.Item2;
            transactionsResponseDto.Transactions.Add(transactionResponseDto);
        }


        return transactionsResponseDto;
    }


    public async Task<(string, string)> ParseIndexerTransactionValueInfoAsync(IndexerTransactionDto transactionDto)
    {
        double value = 0;
        double fee = 0;

        foreach (var indexerLogEventDto in transactionDto.LogEvents)
        {
            indexerLogEventDto.ExtraProperties.TryGetValue("Indexed", out var indexed);
            indexerLogEventDto.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);

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

            switch (indexerLogEventDto.EventName)
            {
                case nameof(Transferred):
                    var transferred = new Transferred();
                    transferred.MergeFrom(logEvent);
                    value += transferred.Symbol == "ELF" ? Convert.ToDouble(transferred.Amount) : 0;
                    break;
                case nameof(TransactionFeeCharged):
                    var transactionFeeCharged = new TransactionFeeCharged();
                    transactionFeeCharged.MergeFrom(logEvent);
                    fee += transactionFeeCharged.Symbol == "ELF"
                        ? Convert.ToDouble(transactionFeeCharged.Amount)
                        : 0;


                    break;
            }
        }

        return (value.ToString(), fee.ToString());
    }

    public async Task SetTransactionInfoAsync(TransactionResponseDto transaction,
        IndexerTransactionDto indexerTransactionDto)
    {
        var transactionValue = 0d;
        var fee = 0d;

        foreach (var txnLogEvent in indexerTransactionDto.LogEvents)
        {
            txnLogEvent.ExtraProperties.TryGetValue("Indexed", out var indexed);
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

            if (txnLogEvent.EventName == nameof(Transferred))
            {
                var transferred = new Transferred();
                transferred.MergeFrom(logEvent);
                transactionValue += transferred.Symbol == "ELF" ? Convert.ToDouble(transferred.Amount) : 0;
            }

            if (txnLogEvent.EventName == nameof(TransactionFeeCharged))
            {
                var transactionFeeCharged = new TransactionFeeCharged();
                transactionFeeCharged.MergeFrom(logEvent);
                fee += transactionFeeCharged.Symbol == "ELF"
                    ? Convert.ToDouble(transactionFeeCharged.Amount)
                    : 0;
            }
        }

        transaction.TransactionValue = transactionValue.ToString();
        transaction.TransactionFee = fee.ToString();
    }


    public async Task SetBlockBurntFee(List<BlockResponseDto> list, string chainId)
    {
        try
        {
            var longs = list.Select(a => a.BlockHeight).ToList();
            var mustQuery = new List<Func<QueryContainerDescriptor<BlockExtraIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Terms(t => t.Field(f => f.BlockHeight).Terms(longs)));
            QueryContainer Filter(QueryContainerDescriptor<BlockExtraIndex> f) => f.Bool(b => b.Must(mustQuery));
            var result = await _blockExtraIndexRepository.GetListAsync(Filter, skip: 0, limit: 10000,
                index: BlockChainIndexNameHelper.GenerateBlockExtraIndexName(chainId));
            if (result.Item2.IsNullOrEmpty())
            {
                return;
            }

            Dictionary<long, long> myDictionary = result.Item2.ToDictionary(x => x.BlockHeight, x => x.BurntFee);

            foreach (var blockDto in list)
            {
                if (myDictionary.TryGetValue(blockDto.BlockHeight, out var value))
                {
                    blockDto.BurntFees = value.ToString();
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SetBlockBurntFee err");
            throw;
        }
    }

    private CommonAddressDto ConvertAddress(string address, string chainId)
    {
        var commonAddressDto = new CommonAddressDto()
        {
            AddressType = AddressType.EoaAddress,
            Address = address
        };
        if (_globalOptions.ContractNames.TryGetValue(chainId, out var contractNames))
        {
            if (contractNames.TryGetValue(address, out var contractName))
            {
                commonAddressDto.Name = contractName;
                commonAddressDto.AddressType = AddressType.ContractAddress;
            }
        }


        return commonAddressDto;
    }

    public IMessage? ParseMessage(string eventName, ByteString byteString)
    {
        IMessage? message = eventName switch
        {
            "Transferred" => Transferred.Parser.ParseFrom(byteString),
            "CrossChainTransferred" => CrossChainTransferred.Parser.ParseFrom(byteString),
            "CrossChainReceived" => CrossChainReceived.Parser.ParseFrom(byteString),
            _ => null
        };
        return message;
    }
}