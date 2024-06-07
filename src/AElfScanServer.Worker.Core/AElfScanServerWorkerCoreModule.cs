using AElf.EntityMapping.Elasticsearch;
using AElf.Indexing.Elasticsearch;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.HttpApi;
using AElfScanServer.BlockChain.HttpApi.DataStrategy;
using AElfScanServer.Options;
using AElfScanServer.Worker.Core.Options;
using AElfScanServer.Worker.Core.Provider;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Worker.Core;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AElfEntityMappingElasticsearchModule),
    typeof(AElfIndexingElasticsearchModule),
    typeof(BlockChainHttpApiModule),
    typeof(AElfScanServerBlockChainModule)
)]
public class AElfScanServerWorkerCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AElfScanServerWorkerCoreModule>(); });
        context.Services.AddTransient<IStorageProvider, StorageProvider>();
        context.Services.AddSingleton<OverviewDataStrategy, OverviewDataStrategy>();
        context.Services.AddSingleton<LatestTransactionDataStrategy, LatestTransactionDataStrategy>();
        var configuration = context.Services.GetConfiguration();
        Configure<PullTransactionChainIdsOptions>(configuration.GetSection("PullTransactionChainIds"));
    }
}