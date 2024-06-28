using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.Domain.Shared.Common;
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


    public Task<InitRoundResp> InitDailyNetwork(SetRoundRequest request);
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


    public async Task<InitRoundResp> InitDailyNetwork(SetRoundRequest request)
    {
        var queryable = await _roundIndexRepository.GetQueryableAsync();
        var initRoundResp = new InitRoundResp();

        queryable = queryable.Where(c => c.ChainId == request.ChainId);

        var indices = queryable.Where(c => c.StartTime >= request.StartDate).Where(c => c.StartTime <= request.EndDate).Take(40000)
            .OrderBy(c => c.RoundNumber).ToList();

        initRoundResp.MinDate = DateTimeHelper.GetDateTimeString(indices.First().StartTime);
        initRoundResp.MaxDate = DateTimeHelper.GetDateTimeString(indices.Last().StartTime);
        initRoundResp.MinRound = indices.First().RoundNumber;
        initRoundResp.MaxRound = indices.Last().RoundNumber;
        initRoundResp.RoundCount = indices.Count;
        initRoundResp.UpdateDate = new List<string>();

        
        if (request.SetNumber > 0)
        {
            await ConnectAsync();
            RedisDatabase.StringSet(RedisKeyHelper.LatestRound(request.ChainId), request.SetNumber);
        }

        if (!request.UpdateData)
        {
            return initRoundResp;
        }

        var curDate = DateTimeHelper.GetDateTimeLong(indices[0].StartTime);
        var list = new List<RoundIndex>();
        list.Add(indices[0]);


        foreach (var roundIndex in indices.GetRange(1, indices.Count - 1))
        {
            var date = DateTimeHelper.GetDateTimeLong(roundIndex.StartTime);
            if (date != curDate)
            {
                await UpdateDailyNetwork(request.ChainId, curDate, list);
                _logger.LogInformation("handler round index chainId:{0},startDate:{1}", request.ChainId,
                    DateTimeHelper.GetDateTimeString(curDate));
                initRoundResp.UpdateDate.Add(DateTimeHelper.GetDateTimeString(curDate));
                curDate = date;
                list = new List<RoundIndex>();
                list.Add(roundIndex);
            }
            else
            {
                list.Add(roundIndex);
            }
        }

        if (!list.IsNullOrEmpty())
        {
            await UpdateDailyNetwork(request.ChainId, curDate, list);
            initRoundResp.UpdateDate.Add(DateTimeHelper.GetDateTimeString(curDate));
        }

        return initRoundResp;
    }

    public async Task UpdateDailyNetwork(string chainId, long todayTotalSeconds, List<RoundIndex> list)
    {
        if (list.IsNullOrEmpty())
        {
            return;
        }

        var blockProduceIndex = new DailyBlockProduceCountIndex()
        {
            Date = todayTotalSeconds,
            ChainId = chainId
        };

        var dailyCycleCountIndex = new DailyCycleCountIndex()
        {
            Date = todayTotalSeconds,
            ChainId = chainId
        };

        var dailyBlockProduceDurationIndex = new DailyBlockProduceDurationIndex()
        {
            Date = todayTotalSeconds,
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

        await _blockProduceIndexRepository.AddOrUpdateAsync(blockProduceIndex);
        await _blockProduceDurationRepository.AddOrUpdateAsync(dailyBlockProduceDurationIndex);
        await _cycleCountRepository.AddOrUpdateAsync(dailyCycleCountIndex);
        _logger.LogInformation("Insert daily network statistic count index chainId:{0},date:{1}", chainId,
            DateTimeHelper.GetDateTimeString(todayTotalSeconds));
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

        var uniqueAddressCounts = JsonConvert.DeserializeObject<List<UniqueAddressCount>>(value);
        for (var i = 0; i < uniqueAddressCounts.Count; i++)
        {
            if (i == 0)
            {
                uniqueAddressCounts[i].TotalUniqueAddressees = uniqueAddressCounts[i].AddressCount;
                continue;
            }

            uniqueAddressCounts[i].TotalUniqueAddressees =
                uniqueAddressCounts[i].AddressCount + uniqueAddressCounts[i - 1].AddressCount;
        }

        uniqueAddressCountResp.List
            = uniqueAddressCounts;
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