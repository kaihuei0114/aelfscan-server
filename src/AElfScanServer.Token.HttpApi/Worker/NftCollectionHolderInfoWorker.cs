using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Dtos;
using AElfScanServer.Entities;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Options;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.TokenDataFunction.Worker;

public class NftCollectionHolderInfoWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<NftCollectionHolderInfoWorker> _logger;
    private readonly INftCollectionHolderProvider _collectionHolderProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly WorkerOptions _workerOptions;

    public NftCollectionHolderInfoWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        ILogger<NftCollectionHolderInfoWorker> logger, INftCollectionHolderProvider collectionHolderProvider,
        ITokenIndexerProvider tokenIndexerProvider, IOptionsSnapshot<WorkerOptions> workerOptions)
        : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _collectionHolderProvider = collectionHolderProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _workerOptions = workerOptions.Value;
        Timer.Period = 1000 * 60 * 60 * 1;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        foreach (var chainInfo in _workerOptions.ChainInfos)
        {
            var tokenListInput = new TokenListInput()
            {
                ChainId = chainInfo.ChainId,
                Types = new List<SymbolType> { SymbolType.Nft_Collection }
            };
            var tokenListDto = await _tokenIndexerProvider.GetTokenListAsync(tokenListInput);
            var collectionSymbols = tokenListDto.Items.Select(token => token.CollectionSymbol).ToList();
            var tasks = collectionSymbols.Select(collectionSymbol => UpdateNftCollectionHolderInfo(chainInfo.ChainId, collectionSymbol));
            await Task.WhenAll(tasks);
        }
    }

    private async Task UpdateNftCollectionHolderInfo(string chainId, string collectionSymbol)
    {
        var batchSize = _workerOptions.BatchSize;
        var skipCount = 0;
        var moreData = true;
        var list = new List<IndexerTokenHolderInfoDto>();
        while (moreData)
        {
            var tokenHolderInput = new TokenHolderInput
            {
                ChainId = chainId,
                SkipCount = skipCount,
                MaxResultCount = batchSize,
                CollectionSymbol = collectionSymbol
            };
            var tokenHolderInfos = await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);

            if (!tokenHolderInfos.Items.Any() || tokenHolderInfos.Items.Count < batchSize)
            {
                moreData = false;
            }
            else
            {
                skipCount += batchSize;
            }
            list.AddRange(tokenHolderInfos.Items);
        }
        var result = list
            .GroupBy(n => n.Address)
            .Select(g => new NftCollectionHolderInfoIndex
            {
                Address = g.Key,
                CollectionSymbol = collectionSymbol,
                Quantity = g.Sum(n => n.Amount),
                FormatQuantity = g.Sum(n => n.FormatAmount),
                ChainId = chainId
            })
            .ToList();
        await _collectionHolderProvider.UpdateNftCollectionHolder(result);
    }
}