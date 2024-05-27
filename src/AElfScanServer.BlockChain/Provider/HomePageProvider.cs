using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Indexing.Elasticsearch;
using AElf.Standards.ACS10;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.Options;
using Elasticsearch.Net;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BlockChain.Provider;

public class HomePageProvider : AbpRedisCache, ISingletonDependency
{
    private readonly INESTRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly INESTRepository<BlockExtraIndex, string> _blockExtraIndexRepository;
    private readonly INESTRepository<AddressIndex, string> _addressIndexRepository;
    private readonly INESTRepository<TokenInfoIndex, string> _tokenInfoIndexRepository;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly IElasticClient _elasticClient;
    private const string TransactionCountRedisKey = "transaction_count";
    private const string AddressCountRedisKey = "address_count";
    private const string BlockHeightRedisKey = "address_count";


    private readonly ILogger<HomePageProvider> _logger;

    public HomePageProvider(INESTRepository<TransactionIndex, string> transactionIndexRepository,
        ILogger<HomePageProvider> logger, IOptionsMonitor<GlobalOptions> blockChainOptions,
        IOptions<ElasticsearchOptions> options,
        INESTRepository<BlockExtraIndex, string> blockExtraIndexRepository,
        INESTRepository<AddressIndex, string> addressIndexRepository,
        INESTRepository<TokenInfoIndex, string> tokenInfoIndexRepository,
        IOptions<RedisCacheOptions> optionsAccessor) : base(optionsAccessor)
    {
        _transactionIndexRepository = transactionIndexRepository;
        _logger = logger;
        _globalOptions = blockChainOptions;
        var uris = options.Value.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
        _blockExtraIndexRepository = blockExtraIndexRepository;
        _addressIndexRepository = addressIndexRepository;
        _tokenInfoIndexRepository = tokenInfoIndexRepository;
        // InitDeque();
    }

    public async Task<long> GetRewardAsync(string chainId)
    {
        try
        {
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.RewardKey(chainId));
            if (!redisValue.IsNullOrEmpty)
            {
                _logger.LogInformation("Get reward from cache,chainId:{c},cache value:{s}", chainId, redisValue);
                return Convert.ToInt64(redisValue);
            }

            var nodeHost = _globalOptions.CurrentValue.ChainNodeHosts[chainId];
            _logger.LogInformation("Get chainId node host,chainId:{c},nodeHost:{n}", chainId, nodeHost);
            var aElfClient = new AElfClient(_globalOptions.CurrentValue.ChainNodeHosts[chainId]);

            var address = (await aElfClient.GetContractAddressByNameAsync(
                HashHelper.ComputeFrom("AElf.ContractNames.Consensus"))).ToBase58();

            var transactionGetCurrentTermMiningReward =
                await aElfClient.GenerateTransactionAsync(
                    aElfClient.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    address,
                    "GetCurrentTermMiningReward", new Empty());

            var signTransaction =
                aElfClient.SignTransaction(GlobalOptions.PrivateKey, transactionGetCurrentTermMiningReward);
            var transactionResult = await aElfClient.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = signTransaction.ToByteArray().ToHex()
            });

            var amount = Int64Value.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(transactionResult)).Value;


            address = (await aElfClient.GetContractAddressByNameAsync(
                HashHelper.ComputeFrom("AElf.ContractNames.Treasury"))).ToBase58();

            var transactionGetUndistributedDividends =
                await aElfClient.GenerateTransactionAsync(
                    aElfClient.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    address,
                    "GetUndistributedDividends", new Empty());


            signTransaction =
                aElfClient.SignTransaction(GlobalOptions.PrivateKey, transactionGetUndistributedDividends);
            transactionResult = await aElfClient.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = signTransaction.ToByteArray().ToHex()
            });
            var dividend = Dividends.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(transactionResult));
            if (dividend.Value.TryGetValue("ELF", out var value))
            {
                amount += value;
            }

            RedisDatabase.StringSet(RedisKeyHelper.RewardKey(chainId), amount,
                TimeSpan.FromSeconds(_globalOptions.CurrentValue.RewardCacheExpiration));
            _logger.LogInformation("Set cache when Get reward from chain,chainId:{c},amount:{a}", chainId, amount);
            return amount;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get reward err,chainId:{c}", chainId);
        }

        return 0;
    }

    public async Task<List<TransactionCountPerMinuteDto>> FindTransactionRateList(string chainId)
    {
        var transactionCountPerMinuteList = new List<TransactionCountPerMinuteDto>();
        try
        {
            await ConnectAsync();
            var redisValues = RedisDatabase.ListRange(RedisKeyHelper.TransactionTPS(chainId), -180, -1);
            if (redisValues.IsNullOrEmpty())
            {
                redisValues = RedisDatabase.ListRange(RedisKeyHelper.TransactionTPS(chainId), -1);
                if (redisValues.IsNullOrEmpty())
                {
                    _logger.LogWarning("double not find tx rate date from redis err,chainId:{c}", chainId);
                    return transactionCountPerMinuteList;
                }
            }

            foreach (var redisValue in redisValues)
            {
                var strings = redisValue.ToString().Split("_");
                if (strings.Length != 2)
                {
                    _logger.LogWarning("tx rate list len is not 2 from redis,chainId:{c},value:{v}", chainId, strings);
                    continue;
                }

                transactionCountPerMinuteList.Add(new TransactionCountPerMinuteDto()
                {
                    Start = Convert.ToInt64(strings[0]),
                    Count = Convert.ToInt32(strings[1])
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get tx rate date from redis err,chainId:{c}", chainId);
        }

        return transactionCountPerMinuteList;
    }


    public async Task<long> GetTransactionCount(string chainId)
    {
        try
        {
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.TransactionChartData(chainId));

            var transactionCountPerMinuteDtos =
                JsonConvert.DeserializeObject<List<TransactionCountPerMinuteDto>>(redisValue);
            if (transactionCountPerMinuteDtos.IsNullOrEmpty())
            {
                _logger.LogWarning("Transaction count per minute redis cache is null chainId:{c}", chainId);
                return 0;
            }

            return transactionCountPerMinuteDtos.Last().Count;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get  transaction count per minute err,chainId:{c}", chainId);
            return 0;
        }
    }
}