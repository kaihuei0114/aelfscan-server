using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.DataStrategy;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Service;
using AElfScanServer.DataStrategy;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.AspNetCore.SignalR;
using Timer = System.Timers.Timer;

namespace AElfScanServer.HttpApi.Hubs;

public class ExploreHub : AbpHub
{
    private readonly IHomePageService _HomePageService;
    private readonly IBlockChainService _blockChainService;
    private readonly IHubContext<ExploreHub> _hubContext;
    private readonly ILogger<ExploreHub> _logger;
    private readonly OptionsMonitor<GlobalOptions> _globalOptions;
    private static Timer _timer = new Timer();
    private readonly DataStrategyContext<string, HomeOverviewResponseDto> _overviewDataStrategy;
    private readonly DataStrategyContext<string, TransactionsResponseDto> _latestTransactionsDataStrategy;
    private readonly DataStrategyContext<string, BlocksResponseDto> _latestBlocksDataStrategy;
    private readonly DataStrategyContext<string, BlockProduceInfoDto> _bpDataStrategy;

    private static readonly ConcurrentDictionary<string, bool>
        _isPushRunning = new ConcurrentDictionary<string, bool>();


    public ExploreHub(IHomePageService homePageService, ILogger<ExploreHub> logger,
        IBlockChainService blockChainService, IHubContext<ExploreHub> hubContext,
        OverviewDataStrategy overviewDataStrategy, LatestTransactionDataStrategy latestTransactionsDataStrategy,
        CurrentBpProduceDataStrategy bpDataStrategy,
        LatestBlocksDataStrategy latestBlocksDataStrategy,OptionsMonitor<GlobalOptions> globalOptions)
    {
        _HomePageService = homePageService;
        _logger = logger;
        _blockChainService = blockChainService;
        _hubContext = hubContext;
        _overviewDataStrategy = new DataStrategyContext<string, HomeOverviewResponseDto>(overviewDataStrategy);
        _latestTransactionsDataStrategy =
            new DataStrategyContext<string, TransactionsResponseDto>(latestTransactionsDataStrategy);
        _latestBlocksDataStrategy =
            new DataStrategyContext<string, BlocksResponseDto>(latestBlocksDataStrategy);
        _bpDataStrategy = new DataStrategyContext<string, BlockProduceInfoDto>(bpDataStrategy);
        _globalOptions = globalOptions;
    }


    public async Task RequestBpProduce(CommonRequest request)
    {
        var startNew = Stopwatch.StartNew();
        var resp = await _bpDataStrategy.DisplayData(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBpProduceGroupName(request.ChainId));
        _logger.LogInformation("RequestBpProduce: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveBpProduce", resp);
        startNew.Stop();
        _logger.LogInformation("RequestBpProduce costTime:{chainId},{costTime}", request.ChainId,
            startNew.Elapsed.TotalSeconds);
        PushRequestBpProduceAsync(request.ChainId);
    }

    public async Task UnsubscribeBpProduce(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBpProduceGroupName(request.ChainId));
    }


    public async Task PushRequestBpProduceAsync(string chainId)
    {
        var key = "bpProduce" + chainId;

        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }

        try
        {
            while (true)
            {
                await Task.Delay(2000);
                var startNew = Stopwatch.StartNew();
                var resp = await _bpDataStrategy.DisplayData(chainId);

                await _hubContext.Clients.Groups(HubGroupHelper.GetBpProduceGroupName(chainId))
                    .SendAsync("ReceiveBpProduce", resp);
                startNew.Stop();
                _logger.LogInformation("PushRequestBpProduceAsync costTime:{chainId},{costTime}", chainId,
                    startNew.Elapsed.TotalSeconds);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("push bp produce error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }


    public async Task RequestMergeBlockInfo(MergeBlockInfoReq request)
    {
        var startNew = Stopwatch.StartNew();
        var transactions = await _latestTransactionsDataStrategy.DisplayData(request.ChainId);
        var blocks = await _latestBlocksDataStrategy.DisplayData(request.ChainId);
        var resp = new WebSocketMergeBlockInfoDto()
        {
            LatestTransactions = transactions,
            LatestBlocks = blocks
        };

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetMergeBlockInfoGroupName(request.ChainId));
        _logger.LogInformation("RequestMergeBlockInfo: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveMergeBlockInfo", resp);

        startNew.Stop();
        _logger.LogInformation("RequestMergeBlockInfo costTime:{chainId},{costTime}", request.ChainId,
            startNew.Elapsed.TotalSeconds);

        PushMergeBlockInfoAsync(request.ChainId);
    }

    public async Task PushMergeBlockInfoAsync(string chainId)
    {
        var key = "mergeBlockInfo" + chainId;
        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }


        try
        {
            while (true)
            {
                await Task.Delay(2000);
                var startNew = Stopwatch.StartNew();
                var transactions = await _latestTransactionsDataStrategy.DisplayData(chainId);
                var blocks = await _latestBlocksDataStrategy.DisplayData(chainId);
                var resp = new WebSocketMergeBlockInfoDto()
                {
                    LatestTransactions = transactions,
                    LatestBlocks = blocks
                };
                await _hubContext.Clients.Groups(HubGroupHelper.GetMergeBlockInfoGroupName(chainId))
                    .SendAsync("ReceiveMergeBlockInfo", resp);
                startNew.Stop();
                _logger.LogInformation("PushMergeBlockInfoAsync costTime:{chainId},{costTime}", chainId,
                    startNew.Elapsed.TotalSeconds);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("push merge block info error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }

    public async Task UnsubscribeMergeBlockInfo(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetMergeBlockInfoGroupName(request.ChainId));
    }


    public async Task RequestBlockchainOverview(BlockchainOverviewRequestDto request)
    {
        var startNew = Stopwatch.StartNew();
   
        var resp = await _overviewDataStrategy.DisplayData(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBlockOverviewGroupName(request.ChainId));
        await Clients.Caller.SendAsync("ReceiveBlockchainOverview", resp);
        PushBlockOverViewAsync(request.ChainId);

        startNew.Stop();
        _logger.LogInformation("RequestBlockchainOverview costTime:{chainId},{costTime}", request.ChainId,
            startNew.Elapsed.TotalSeconds);
    }

    public async Task UnsubscribeBlockchainOverview(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBlockOverviewGroupName(request.ChainId));
    }


    public async Task PushBlockOverViewAsync(string chainId)
    {
        var key = "overview" + chainId;
        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }


        try
        {
            while (true)
            {
                await Task.Delay(2000);
                var startNew = Stopwatch.StartNew();
                var resp = await _overviewDataStrategy.DisplayData(chainId);
                await _hubContext.Clients.Groups(HubGroupHelper.GetBlockOverviewGroupName(chainId))
                    .SendAsync("ReceiveBlockchainOverview", resp);

                startNew.Stop();
                _logger.LogInformation("PushBlockOverViewAsync costTime:{chainId},{costTime}", chainId,
                    startNew.Elapsed.TotalSeconds);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("push block overview error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }


    public async Task RequestTransactionDataChart(GetTransactionPerMinuteRequestDto request)
    {
        var startNew = Stopwatch.StartNew();
        var resp = await _HomePageService.GetTransactionPerMinuteAsync(request.ChainId);

        resp.All = resp.All.Take(resp.All.Count - 3).ToList();
        resp.Owner = resp.Owner.Take(resp.Owner.Count - 3).ToList();
        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetTransactionCountPerMinuteGroupName(request.ChainId));

        _logger.LogInformation("RequestTransactionDataChart: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveTransactionDataChart", resp);
        PushTransactionCountPerMinuteAsync(request.ChainId);

        startNew.Stop();
        _logger.LogInformation("RequestTransactionDataChart costTime:{chainId},{costTime}", request.ChainId,
            startNew.Elapsed.TotalSeconds);
    }


    public async Task UnsubscribeTransactionDataChart(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetTransactionCountPerMinuteGroupName(request.ChainId));
    }


    public async Task PushTransactionCountPerMinuteAsync(string chainId)
    {
        var key = "transactionCountPerMinute" + chainId;
        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }

        try
        {
            while (true)
            {
                await Task.Delay(60 * 1000);
                var startNew = Stopwatch.StartNew();
                var resp = await _HomePageService.GetTransactionPerMinuteAsync(chainId);
                resp.All = resp.All.Take(resp.All.Count - 3).ToList();
                resp.Owner = resp.Owner.Take(resp.Owner.Count - 3).ToList();
                await _hubContext.Clients.Groups(HubGroupHelper.GetTransactionCountPerMinuteGroupName(chainId))
                    .SendAsync("ReceiveTransactionDataChart", resp);
                startNew.Stop();
                _logger.LogInformation("PushTransactionCountPerMinuteAsync costTime:{chainId},{costTime}", chainId,
                    startNew.Elapsed.TotalSeconds);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Push transaction count per minute error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }
}