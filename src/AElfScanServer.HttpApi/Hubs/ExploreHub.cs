using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.DataStrategy;
using AElfScanServer.HttpApi.Helper;
using AElfScanServer.HttpApi.Service;
using AElfScanServer.DataStrategy;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.SignalR;
using Timer = System.Timers.Timer;

namespace AElfScanServer.HttpApi.Hubs;

public class ExploreHub : AbpHub
{
    private readonly IHomePageService _HomePageService;
    private readonly IBlockChainService _blockChainService;
    private readonly IHubContext<ExploreHub> _hubContext;
    private readonly ILogger<ExploreHub> _logger;
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
        LatestBlocksDataStrategy latestBlocksDataStrategy)
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
    }


    public async Task RequestBpProduce(CommonRequest request)
    {
        var resp = await _bpDataStrategy.DisplayData(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBpProduceGroupName(request.ChainId));
        _logger.LogInformation("RequestBpProduce: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveBpProduce", resp);

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

                var resp = await _bpDataStrategy.DisplayData(chainId);

                await _hubContext.Clients.Groups(HubGroupHelper.GetBpProduceGroupName(chainId))
                    .SendAsync("ReceiveBpProduce", resp);
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


    public async Task RequestLatestTransactions(LatestTransactionsReq request)
    {
        var resp = await _latestTransactionsDataStrategy.DisplayData(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestTransactionsGroupName(request.ChainId));
        _logger.LogInformation("RequestLatestTransactions: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveLatestTransactions", resp);

        PushLatestTransactionsAsync(request.ChainId);
    }

    public async Task UnsubscribeLatestTransactions(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestTransactionsGroupName(request.ChainId));
    }


    public async Task PushLatestTransactionsAsync(string chainId)
    {
        var key = "transaction" + chainId;
        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }


        try
        {
            while (true)
            {
                await Task.Delay(2000);
                var resp = await _latestTransactionsDataStrategy.DisplayData(chainId);
                await _hubContext.Clients.Groups(HubGroupHelper.GetLatestTransactionsGroupName(chainId))
                    .SendAsync("ReceiveLatestTransactions", resp);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("push transaction error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }


    public async Task RequestBlockchainOverview(BlockchainOverviewRequestDto request)
    {
        var resp = await _overviewDataStrategy.DisplayData(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBlockOverviewGroupName(request.ChainId));
        await Clients.Caller.SendAsync("ReceiveBlockchainOverview", resp);
        PushBlockOverViewAsync(request.ChainId);
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
                var resp = await _overviewDataStrategy.DisplayData(chainId);
                await _hubContext.Clients.Groups(HubGroupHelper.GetBlockOverviewGroupName(chainId))
                    .SendAsync("ReceiveBlockchainOverview", resp);
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


    public async Task RequestLatestBlocks(LatestBlocksRequestDto request)
    {
        var resp = await _latestBlocksDataStrategy.DisplayData(request.ChainId);


        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestBlocksGroupName(request.ChainId));

        await Clients.Caller.SendAsync("ReceiveLatestBlocks", resp);

        PushLatestBlocksAsync(request.ChainId);
    }

    public async Task UnsubscribeRequestLatestBlocks(CommonRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestBlocksGroupName(request.ChainId));
    }


    public async Task PushLatestBlocksAsync(string chainId)
    {
        var key = "block" + chainId;
        if (!_isPushRunning.TryAdd(key, true))
        {
            return;
        }


        try
        {
            while (true)
            {
                await Task.Delay(2000);
                var resp = await _latestBlocksDataStrategy.DisplayData(chainId);
                if (resp.Blocks.Count > 6)
                {
                    resp.Blocks = resp.Blocks.GetRange(0, 6);
                }

                await _hubContext.Clients.Groups(HubGroupHelper.GetLatestBlocksGroupName(chainId))
                    .SendAsync("ReceiveLatestBlocks", resp);
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Push blocks error: {error}", e);
        }
        finally
        {
            _isPushRunning.TryRemove(key, out var v);
        }
    }


    public async Task RequestTransactionDataChart(GetTransactionPerMinuteRequestDto request)
    {
        var resp = await _HomePageService.GetTransactionPerMinuteAsync(request.ChainId);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetTransactionCountPerMinuteGroupName(request.ChainId));

        _logger.LogInformation("RequestTransactionDataChart: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveTransactionDataChart", resp);
        PushTransactionCountPerMinuteAsync(request.ChainId);
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
                var resp = await _HomePageService.GetTransactionPerMinuteAsync(chainId);

                await _hubContext.Clients.Groups(HubGroupHelper.GetTransactionCountPerMinuteGroupName(chainId))
                    .SendAsync("ReceiveTransactionDataChart", resp);
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