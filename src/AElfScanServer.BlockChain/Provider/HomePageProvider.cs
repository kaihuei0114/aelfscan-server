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
using AElfScanServer.Common.Options;
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
    }

    public async Task<long> GetRewardAsync(string chainId)
    {
        try
        {
            if (chainId != "AELF")
            {
                return 0;
            }

            _logger.LogInformation("Get reward,chainId:{c}", chainId);
            await ConnectAsync();
            var redisValue = RedisDatabase.StringGet(RedisKeyHelper.RewardKey(chainId));
            if (!redisValue.IsNullOrEmpty)
            {
                _logger.LogInformation("Get reward from cache,chainId:{c},cache value:{s}", chainId, redisValue);
                return Convert.ToInt64(redisValue);
            }

            var nodeHost = _globalOptions.CurrentValue.ChainNodeHosts[chainId];
            _logger.LogInformation("Get chainId node host,chainId:{c},nodeHost:{n}", chainId, nodeHost);
            var aElfClient = new AElfClient(nodeHost);

            if (_globalOptions.CurrentValue.ConsensusContractAddress.IsNullOrEmpty())
            {
                _logger.LogWarning("ConsensusContractAddress is null");
                return 0;
            }


            var transactionGetCurrentTermMiningReward =
                await aElfClient.GenerateTransactionAsync(
                    aElfClient.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    _globalOptions.CurrentValue.ConsensusContractAddress,
                    "GetCurrentTermMiningReward", new Empty());

            var signTransaction =
                aElfClient.SignTransaction(GlobalOptions.PrivateKey, transactionGetCurrentTermMiningReward);
            var transactionResult = await aElfClient.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = signTransaction.ToByteArray().ToHex()
            });

            var amount = Int64Value.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(transactionResult)).Value;

            if (_globalOptions.CurrentValue.TreasuryContractAddress.IsNullOrEmpty())
            {
                _logger.LogWarning("TreasuryContractAddress is null");
                return 0;
            }


            var transactionGetUndistributedDividends =
                await aElfClient.GenerateTransactionAsync(
                    aElfClient.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    _globalOptions.CurrentValue.TreasuryContractAddress,
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