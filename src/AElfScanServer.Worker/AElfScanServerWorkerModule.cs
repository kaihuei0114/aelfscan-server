using System;
using System.Collections.Generic;
using System.Linq;
using AElf.EntityMapping.Elasticsearch;
using AElf.Indexing.Elasticsearch;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.TokenDataFunction.Provider;
using AElfScanServer.Worker.Core;
using AElfScanServer.Worker.Core.Options;
using AElfScanServer.Worker.Core.Service;
using AElfScanServer.Worker.Core.Worker;
using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Volo.Abp;
using Volo.Abp.AspNetCore.SignalR;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace AElfScanServer.Worker;

[DependsOn(
    typeof(AElfScanServerWorkerCoreModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAutofacModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpAspNetCoreSignalRModule),
    typeof(AElfScanServerBlockChainModule),
    typeof(AElfEntityMappingElasticsearchModule),
    typeof(AElfIndexingElasticsearchModule)
)]
public class AElfScanServerWorkerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        ConfigureEsIndex(configuration);
        ConfigureContractNameIndex(configuration);
        context.Services.AddSingleton<ITransactionService, TransactionService>();
        context.Services.AddSingleton<ITokenIndexerProvider, TokenIndexerProvider>();
        context.Services.AddSingleton<INftInfoProvider, NftInfoProvider>();
        context.Services.AddSingleton<ITokenAssetProvider, TokenAssetProvider>();
        context.Services.AddSingleton<ITokenPriceService, TokenPriceService>();
        context.Services.AddSingleton<ITokenAssetProvider, TokenAssetProvider>();

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "AElfScanWorker:"; });
        Configure<BlockChainProducerInfoSyncWorkerOptions>(configuration.GetSection("BlockChainProducer"));
        Configure<ContractInfoSyncWorkerOptions>(configuration.GetSection("Contract"));
        Configure<WorkerOptions>(configuration.GetSection("Worker"));
        context.Services.AddHostedService<AElfScanServerHostedService>();
        context.Services.AddHttpClient();
    }


    public void ConfigureContractNameIndex(IConfiguration configuration)
    {
        var blockChainOptions = configuration.GetSection("BlockChain").Get<GlobalOptions>();
        var indexerOptions = configuration.GetSection("AELFIndexer").Get<AELFIndexerOptions>();
        var elasticsearchOptions = configuration.GetSection("Elasticsearch").Get<ElasticsearchOptions>();
        var uris = elasticsearchOptions.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        var elasticClient = new ElasticClient(settings);
        foreach (var indexerOptionsChainId in indexerOptions.ChainIds)
        {
            if (blockChainOptions.ContractNames.TryGetValue(indexerOptionsChainId, out var value))
            {
                var updateAddress = new List<AddressIndex>();
                var searchResponse = elasticClient.Search<AddressIndex>(s => s
                    .Index(BlockChainIndexNameHelper.GenerateAddressIndexName(indexerOptionsChainId))
                    .Query(q => q
                        .Terms(t => t
                            .Field(f => f.Address)
                            .Terms(value.Keys.ToList())
                        )
                    )
                );

                if (searchResponse.IsValid)
                {
                    foreach (var addressIndex in searchResponse.Documents)
                    {
                        if (value.TryGetValue(addressIndex.Address, out var contractName))
                        {
                            addressIndex.Name = contractName;
                            addressIndex.LowerName = contractName.ToLower();
                            updateAddress.Add(addressIndex);
                        }
                    }
                }
                else
                {
                    throw new Exception($"Failed to index object: {searchResponse.DebugInformation}");
                }


                if (!updateAddress.IsNullOrEmpty())
                {
                    var updateResponse = elasticClient.Bulk(b => b
                        .Index(BlockChainIndexNameHelper.GenerateAddressIndexName(indexerOptionsChainId))
                        .UpdateMany<AddressIndex>(updateAddress, (descriptor, addressIndex) => descriptor
                            .Doc(addressIndex)
                            .DocAsUpsert()
                        )
                    );

                    if (!updateResponse.IsValid)
                    {
                        throw new Exception($"Failed to index object: {updateResponse.DebugInformation}");
                    }
                }
            }
        }
    }

    public void ConfigureEsIndex(IConfiguration configuration)
    {
        var indexerOptions = configuration.GetSection("AELFIndexer").Get<AELFIndexerOptions>();
        var elasticsearchOptions = configuration.GetSection("Elasticsearch").Get<ElasticsearchOptions>();
        var uris = elasticsearchOptions.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        var elasticClient = new ElasticClient(settings);

        foreach (var indexerOptionsChainId in indexerOptions.ChainIds)
        {
            if (!elasticClient.Indices.Exists(BlockChainIndexNameHelper.GenerateTokenIndexName(indexerOptionsChainId))
                    .Exists)
            {
                var indexResponse = elasticClient.Indices.Create(
                    BlockChainIndexNameHelper.GenerateTokenIndexName(indexerOptionsChainId),
                    c => c.Map<TokenInfoIndex>(m => m.AutoMap()));

                if (!indexResponse.IsValid)
                {
                    throw new Exception($"Failed to index object: {indexResponse.DebugInformation}");
                }
            }

            if (!elasticClient.Indices.Exists(BlockChainIndexNameHelper.GenerateAddressIndexName(indexerOptionsChainId))
                    .Exists)
            {
                var indexResponse = elasticClient.Indices.Create(
                    BlockChainIndexNameHelper.GenerateAddressIndexName(indexerOptionsChainId),
                    c => c.Map<AddressIndex>(m => m.AutoMap()));
                if (!indexResponse.IsValid)
                {
                    throw new Exception($"Failed to index object: {indexResponse.DebugInformation}");
                }
            }

            if (!elasticClient.Indices
                    .Exists(BlockChainIndexNameHelper.GenerateTransactionIndexName(indexerOptionsChainId))
                    .Exists)
            {
                var blockChainOptions = configuration.GetSection("BlockChain").Get<GlobalOptions>();
                var indexResponse = elasticClient.Indices.Create(
                    BlockChainIndexNameHelper.GenerateTransactionIndexName(indexerOptionsChainId), c => c
                        .Settings(s => s
                            .Setting("max_result_window", blockChainOptions.TransactionListMaxCount)
                        )
                        .Map<TransactionIndex>(m => m.AutoMap()));
                if (!indexResponse.IsValid)
                {
                    throw new Exception($"Failed to index object: {indexResponse.DebugInformation}");
                }
            }


            if (!elasticClient.Indices
                    .Exists(BlockChainIndexNameHelper.GenerateLogEventIndexName(indexerOptionsChainId))
                    .Exists)
            {
                var indexResponse = elasticClient.Indices.Create(
                    BlockChainIndexNameHelper.GenerateLogEventIndexName(indexerOptionsChainId),
                    c => c.Map<LogEventIndex>(m => m.AutoMap()));
                if (!indexResponse.IsValid)
                {
                    throw new Exception($"Failed to index object: {indexResponse.DebugInformation}");
                }
            }
        }
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.AddBackgroundWorkerAsync<TransactionRatePerMinuteWorker>();
        context.AddBackgroundWorkerAsync<AddressAssetCalcWorker>();
        context.AddBackgroundWorkerAsync<HomePageOverviewWorker>();
        context.AddBackgroundWorkerAsync<LatestTransactionsWorker>();
        context.AddBackgroundWorkerAsync<LatestBlocksWorker>();
    }
}