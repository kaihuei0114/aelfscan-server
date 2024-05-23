using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.HttpApi.Helper;
using AElfScanServer.BlockChain.HttpApi.Service;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.SignalR;
using Timer = System.Timers.Timer;

namespace AElfScanServer.BlockChainDataFunction.Hubs;

public class ExploreHub : AbpHub
{
    private readonly IHomePageService _HomePageService;
    private readonly IBlockChainService _blockChainService;
    private readonly IHubContext<ExploreHub> _hubContext;
    private readonly ILogger<ExploreHub> _logger;
    private static Timer _timer = new Timer();
    private static readonly object _lockTransactionsObject = new object();
    private static readonly object _lockBlocksObject = new object();
    private static readonly object _lockBlockOverviewObject = new object();
    private static readonly object _lockTransactionCountPerMinuteObject = new object();
    private static bool _isPushTransactionsRunning = false;
    private static bool _isPushBlocksRunning = false;
    private static bool _isPushBlockOverviewRunning = false;
    private static bool _isPushTransactionCountPerMinuteRunning = false;

    public ExploreHub(IHomePageService homePageService, ILogger<ExploreHub> logger,
        IBlockChainService blockChainService, IHubContext<ExploreHub> hubContext)
    {
        _HomePageService = homePageService;
        _logger = logger;
        _blockChainService = blockChainService;
        _hubContext = hubContext;
    }


    public async Task RequestLatestTransactions(LatestTransactionsReq request)
    {
        var resp = await _blockChainService.GetTransactionsAsync(new TransactionsRequestDto()
        {
            ChainId = request.ChainId,
            MaxResultCount = 6
        });

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestTransactionsGroupName(request.ChainId));
        _logger.LogInformation("RequestLatestTransactions: {chainId}", request.ChainId);
        await Clients.Caller.SendAsync("ReceiveLatestTransactions", resp);

        PushLatestTransactionsAsync(request.ChainId);
    }


    public async Task PushLatestTransactionsAsync(string chainId)
    {
        lock (_lockTransactionsObject)
        {
            if (_isPushTransactionsRunning)
            {
                return;
            }

            _isPushTransactionsRunning = true;
        }

        while (true)
        {
            Thread.Sleep(2000);

            try
            {
                var resp = await _blockChainService.GetTransactionsAsync(new TransactionsRequestDto()
                {
                    ChainId = chainId,
                    MaxResultCount = 6
                });

                await _hubContext.Clients.Groups(HubGroupHelper.GetLatestTransactionsGroupName(chainId))
                    .SendAsync("ReceiveLatestTransactions", resp);
            }
            catch (Exception e)
            {
                _logger.LogError("push transaction error: {error}", e.Message);
            }
        }
    }

    public async Task UnsubscribeLatestBlocks(string chainId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestBlocksGroupName(chainId));
    }

    public async Task UnsubscribeLatestTransactions(string chainId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestTransactionsGroupName(chainId));
    }


    public async Task RequestBlockchainOverview(BlockchainOverviewRequestDto request)
    {
        var resp = await _HomePageService.GetBlockchainOverviewAsync(request);

        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetBlockOverviewGroupName(request.ChainId));
        await Clients.Caller.SendAsync("ReceiveBlockchainOverview", resp);
        PushBlockOverViewAsync(request.ChainId);
    }

    public async Task PushBlockOverViewAsync(string chainId)
    {
        if (_isPushBlocksRunning)
        {
            return;
        }

        lock (_lockBlockOverviewObject)
        {
            if (_isPushBlockOverviewRunning)
            {
                return;
            }

            _isPushBlockOverviewRunning = true;
        }


        while (true)
        {
            Thread.Sleep(3000);

            try
            {
                var resp = await _HomePageService.GetBlockchainOverviewAsync(new BlockchainOverviewRequestDto()
                {
                    ChainId = chainId
                });


                await _hubContext.Clients.Groups(HubGroupHelper.GetBlockOverviewGroupName(chainId))
                    .SendAsync("ReceiveBlockchainOverview", resp);
            }
            catch (Exception e)
            {
                _logger.LogError("push block overview error: {error}", e.Message);
            }
        }
    }


    public async Task RequestLatestBlocks(LatestBlocksRequestDto request)
    {
        var resp = await _blockChainService.GetBlocksAsync(new BlocksRequestDto()
        {
            ChainId = request.ChainId,
            MaxResultCount = 6
        });


        await Groups.AddToGroupAsync(Context.ConnectionId,
            HubGroupHelper.GetLatestBlocksGroupName(request.ChainId));

        await Clients.Caller.SendAsync("ReceiveLatestBlocks", resp);

        PushLatestBlocksAsync(request.ChainId);
    }


    public async Task PushLatestBlocksAsync(string chainId)
    {
        if (_isPushBlocksRunning)
        {
            return;
        }

        lock (_lockBlocksObject)
        {
            if (_isPushBlocksRunning)
            {
                return;
            }

            _isPushBlocksRunning = true;
        }

        while (true)
        {
            Thread.Sleep(2000);

            try
            {
                var resp = await _blockChainService.GetBlocksAsync(new BlocksRequestDto()
                {
                    ChainId = chainId,
                    MaxResultCount = 10
                });

                if (resp.Blocks.Count > 6)
                {
                    resp.Blocks = resp.Blocks.GetRange(0, 6);
                }

                await _hubContext.Clients.Groups(HubGroupHelper.GetLatestBlocksGroupName(chainId))
                    .SendAsync("ReceiveLatestBlocks", resp);
            }
            catch (Exception e)
            {
                _logger.LogError("Push blocks error: {error}", e.Message);
            }
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


    public async Task PushTransactionCountPerMinuteAsync(string chainId)
    {
        if (_isPushTransactionCountPerMinuteRunning)
        {
            return;
        }

        lock (_lockTransactionCountPerMinuteObject)
        {
            if (_isPushTransactionCountPerMinuteRunning)
            {
                return;
            }

            _isPushTransactionCountPerMinuteRunning = true;
        }

        while (true)
        {
            Thread.Sleep(60 * 1000);

            try
            {
                var resp = await _HomePageService.GetTransactionPerMinuteAsync(chainId);

                await _hubContext.Clients.Groups(HubGroupHelper.GetTransactionCountPerMinuteGroupName(chainId))
                    .SendAsync("ReceiveTransactionDataChart", resp);
            }
            catch (Exception e)
            {
                _logger.LogError("Push transaction count per minute error: {error}", e.Message);
            }
        }
    }
}