using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos.ChartData;
using AElfScanServer.HttpApi.Helper;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.HttpApi.Service;

public interface IChartDataService
{
    public Task<DailyTransactionCountResp> GetDailyTransactionCountAsync(ChartDataRequest request);


    public Task<UniqueAddressCountResp> GetUniqueAddressCountAsync(ChartDataRequest request);


    public Task<ActiveAddressCountResp> GetActiveAddressCountAsync(ChartDataRequest request);

    public Task<BlockProduceRateResp> GetBlockProduceRateAsync(ChartDataRequest request);

    public Task<AvgBlockDurationResp> GetAvgBlockDurationRespAsync(ChartDataRequest request);

    public Task<CycleCountResp> GetCycleCountRespAsync(ChartDataRequest request);

    public Task<NodeBlockProduceResp> GetNodeBlockProduceRespAsync(ChartDataRequest request);


    public Task<string> SetRoundNumberAsync(SetRoundRequest request);

    public Task<long> GetRoundNumberAsync(SetRoundRequest request);
}

public class ChartDataService : AbpRedisCache, IChartDataService, ITransientDependency
{
    private readonly ILogger<ChartDataService> _logger;
    private readonly IEntityMappingRepository<RoundIndex, string> _roundIndexRepository;
    private readonly IEntityMappingRepository<NodeBlockProduceIndex, string> _nodeBlockProduceIndex;
    private readonly IEntityMappingRepository<DailyBlockProduceCountIndex, string> _blockProduceIndexRepository;
    private readonly IEntityMappingRepository<DailyBlockProduceDurationIndex, string> _blockProduceDurationRepository;
    private readonly IEntityMappingRepository<DailyCycleCountIndex, string> _cycleCountRepository;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly IObjectMapper _objectMapper;

    public ChartDataService(IOptions<RedisCacheOptions> optionsAccessor, ILogger<ChartDataService> logger,
        IEntityMappingRepository<RoundIndex, string> roundIndexRepository,
        IEntityMappingRepository<NodeBlockProduceIndex, string> nodeBlockProduceIndex,
        IEntityMappingRepository<DailyBlockProduceCountIndex, string> blockProduceIndexRepository,
        IObjectMapper objectMapper,
        IEntityMappingRepository<DailyBlockProduceDurationIndex, string> blockProduceDurationRepository,
        IEntityMappingRepository<DailyCycleCountIndex, string> cycleCountRepository,
        IOptionsMonitor<GlobalOptions> globalOptions) : base(
        optionsAccessor)
    {
        _logger = logger;
        _roundIndexRepository = roundIndexRepository;
        _nodeBlockProduceIndex = nodeBlockProduceIndex;
        _blockProduceIndexRepository = blockProduceIndexRepository;
        _objectMapper = objectMapper;
        _blockProduceDurationRepository = blockProduceDurationRepository;
        _cycleCountRepository = cycleCountRepository;
        _globalOptions = globalOptions;
    }


    public async Task<string> SetRoundNumberAsync(SetRoundRequest request)
    {
        await ConnectAsync();

        var redisValue = RedisDatabase.StringSet(RedisKeyHelper.LatestRound(request.ChainId), request.RoundNumber);
        return redisValue.ToString();
    }

    public async Task<long> GetRoundNumberAsync(SetRoundRequest request)
    {
        await ConnectAsync();

        var redisValue = RedisDatabase.StringGet(RedisKeyHelper.LatestRound(request.ChainId));


        if (request.SetNumber)
        {
            RedisDatabase.StringSet(RedisKeyHelper.LatestRound(request.ChainId), request.RoundNumber);
        }

        return (long)redisValue;
    }

    public async Task<NodeBlockProduceResp> GetNodeBlockProduceRespAsync(ChartDataRequest request)
    {
        var blockQueryable = await _nodeBlockProduceIndex.GetQueryableAsync();
        if (request.StartDate > 0 && request.EndDate > 0)
        {
            blockQueryable = blockQueryable.Where(c => c.StartTime >= request.StartDate)
                .Where(c => c.StartTime < request.EndDate);
        }

        var nodeBlockProduceResp = new NodeBlockProduceResp();

        var nodeBlockProduces = new Dictionary<string, NodeBlockProduce>();
        var nodeBlockProduceIndices = blockQueryable.Where(c => c.ChainId == request.ChainId).Take(10000).ToList();
        foreach (var nodeBlockProduceIndex in nodeBlockProduceIndices)
        {
            var address = nodeBlockProduceIndex.NodeAddress;
            if (nodeBlockProduces.TryGetValue(address, out var v))
            {
                v.TotalCycle++;
                v.Blocks += nodeBlockProduceIndex.Blcoks;
                v.MissedBlocks += nodeBlockProduceIndex.MissedBlocks;
            }
            else
            {
                nodeBlockProduces[address] = new NodeBlockProduce()
                {
                    NodeAddress = address,
                    NodeName = await GetContractName(request.ChainId, address),
                    TotalCycle = 1,
                    Blocks = nodeBlockProduceIndex.Blcoks,
                    MissedBlocks = nodeBlockProduceIndex.MissedBlocks
                };
            }
        }

        var cycleQueryable = await _cycleCountRepository.GetQueryableAsync();
        if (request.StartDate > 0 && request.EndDate > 0)
        {
            cycleQueryable = cycleQueryable.Where(c => c.Date >= request.StartDate && c.Date <= request.EndDate);
        }

        var cycleCountIndices = cycleQueryable.Where(c => c.ChainId == request.ChainId);

        var dailyCycleCountIndices = cycleCountIndices.ToList();

        var totalCycle = dailyCycleCountIndices.Select(c => c.CycleCount).Sum();

        var nodeExpectBlocks = totalCycle * 8;

        var blockProduces = nodeBlockProduces.Values.Select(c => c).OrderByDescending(c => c.Blocks).ToList();

        foreach (var nodeBlockProduce in blockProduces)
        {
            nodeBlockProduce.BlocksRate = (nodeBlockProduce.Blocks / nodeExpectBlocks).ToString("F2");
            nodeBlockProduce.CycleRate = (nodeBlockProduce.TotalCycle / totalCycle).ToString("F2");
        }

        nodeBlockProduceResp.List = blockProduces;
        return nodeBlockProduceResp;

        // var roundQueryable = await _roundIndexRepository.GetQueryableAsync();
        // if (request.StartDate > 0 && request.EndDate > 0)
        // {
        //     roundQueryable = roundQueryable.Where(c => c.StartTime >= request.StartDate)
        //         .Where(c => c.StartTime <= request.EndDate);
        // }
        //
        // var roundIndices = roundQueryable.Where(c => c.ChainId == request.ChainId).ToList();
        //
        //
        // var totalCycle = 0l;
        //
        // foreach (var roundIndex in roundIndices)
        // {
        //     
        // }
    }

    public async Task<BlockProduceRateResp> GetBlockProduceRateAsync(ChartDataRequest request)
    {
        var queryableAsync = await _blockProduceIndexRepository.GetQueryableAsync();

        if (request.StartDate > 0 && request.EndDate > 0)
        {
            // queryableAsync = queryableAsync.Where(c=>)
        }

        var list = queryableAsync.Where(c => c.ChainId == request.ChainId).OrderBy(c => c.Date).Skip(0).Take(1000)
            .ToList();


        var destination = _objectMapper.Map<List<DailyBlockProduceCountIndex>, List<DailyBlockProduceCount>>(list);

        var orderList = destination.OrderBy(c => c.BlockProductionRate);

        var blockProduceRateResp = new BlockProduceRateResp()
        {
            List = destination,
            HighestBlockProductionRate = orderList.Last(),
            lowestBlockProductionRate = orderList.First()
        };

        return blockProduceRateResp;
    }

    public async Task<AvgBlockDurationResp> GetAvgBlockDurationRespAsync(ChartDataRequest request)
    {
        var queryableAsync = await _blockProduceDurationRepository.GetQueryableAsync();


        var list = queryableAsync.Where(c => c.ChainId == request.ChainId).OrderBy(c => c.Date).Take(10000)
            .ToList();


        var destination =
            _objectMapper.Map<List<DailyBlockProduceDurationIndex>, List<DailyBlockProduceDuration>>(list);


        var orderList = destination.OrderBy(c => c.AvgBlockDuration);

        var durationResp = new AvgBlockDurationResp()
        {
            List = destination,
            HighestAvgBlockDuration = orderList.Last(),
            LowestBlockProductionRate = orderList.First()
        };

        return durationResp;
    }

    public async Task<CycleCountResp> GetCycleCountRespAsync(ChartDataRequest request)
    {
        var queryableAsync = await _cycleCountRepository.GetQueryableAsync();


        var list = queryableAsync.Where(c => c.ChainId == request.ChainId).OrderBy(c => c.Date).Skip(0).Take(1000)
            .ToList();


        var destination =
            _objectMapper.Map<List<DailyCycleCountIndex>, List<DailyCycleCount>>(list);

        var orderList = destination.OrderBy(c => c.CycleCount);

        var cycleCountResp = new CycleCountResp()
        {
            List = destination,
            HighestMissedCycle = orderList.Last(),
        };

        return cycleCountResp;
    }

    public async Task<DailyTransactionCountResp> GetDailyTransactionCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var dailyTransactionCountResp = new DailyTransactionCountResp()
        {
            ChainId = request.ChainId,
            List = new List<DailyTransactionCount>()
        };
        var key = RedisKeyHelper.DailyTransactionCount(request.ChainId);
        var value = RedisDatabase.StringGet(key);

        dailyTransactionCountResp.List
            = JsonConvert.DeserializeObject<List<DailyTransactionCount>>(value);

        dailyTransactionCountResp.HighestTransactionCount =
            dailyTransactionCountResp.List.MaxBy(c => c.TransactionCount);

        dailyTransactionCountResp.LowesTransactionCount =
            dailyTransactionCountResp.List.MinBy(c => c.TransactionCount);

        return dailyTransactionCountResp;
    }

    public async Task<UniqueAddressCountResp> GetUniqueAddressCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var uniqueAddressCountResp = new UniqueAddressCountResp()
        {
            ChainId = request.ChainId,
            List = new List<UniqueAddressCount>()
        };
        var key = RedisKeyHelper.UniqueAddresses(request.ChainId);
        var value = RedisDatabase.StringGet(key);

        uniqueAddressCountResp.List
            = JsonConvert.DeserializeObject<List<UniqueAddressCount>>(value);

        uniqueAddressCountResp.HighestIncrease =
            uniqueAddressCountResp.List.MaxBy(c => c.AddressCount);

        uniqueAddressCountResp.LowestIncrease =
            uniqueAddressCountResp.List.MinBy(c => c.AddressCount);

        return uniqueAddressCountResp;
    }

    public async Task<ActiveAddressCountResp> GetActiveAddressCountAsync(ChartDataRequest request)
    {
        await ConnectAsync();

        var activeAddressCountResp = new ActiveAddressCountResp()
        {
            ChainId = request.ChainId,
            List = new List<DailyActiveAddressCount>()
        };
        var value = RedisDatabase.StringGet(RedisKeyHelper.DailyActiveAddresses(request.ChainId));

        activeAddressCountResp.List
            = JsonConvert.DeserializeObject<List<DailyActiveAddressCount>>(value);

        activeAddressCountResp.HighestActiveCount =
            activeAddressCountResp.List.MaxBy(c => c.AddressCount);

        activeAddressCountResp.LowestActiveCount =
            activeAddressCountResp.List.MinBy(c => c.AddressCount);

        return activeAddressCountResp;
    }


    public async Task<string> GetContractName(string chainId, string address)
    {
        _globalOptions.CurrentValue.ContractNames.TryGetValue(chainId, out var contractNames);
        if (contractNames == null)
        {
            return "";
        }

        contractNames.TryGetValue(address, out var contractName);

        return contractName;
    }
}