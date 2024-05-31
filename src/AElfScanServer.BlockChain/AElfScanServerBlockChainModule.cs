using AElf.EntityMapping.Elasticsearch;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AElfScanServer.BlockChain;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AElfEntityMappingElasticsearchModule),
    typeof(AElfScanCommonModule),
    typeof(AbpCachingStackExchangeRedisModule)
)]
public class AElfScanServerBlockChainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<BlockChainOption>(context.Services.GetConfiguration().GetSection("BlockChainServer"));


        Configure<AELFIndexerOptions>(configuration.GetSection("AELFIndexer"));
        Configure<GlobalOptions>(configuration.GetSection("BlockChain"));
        Configure<ElasticsearchOptions>(configuration.GetSection("Elasticsearch"));


        context.Services.AddSingleton<AELFIndexerProvider, AELFIndexerProvider>();
        context.Services.AddSingleton<HomePageProvider, HomePageProvider>();
        context.Services.AddSingleton<LogEventProvider, LogEventProvider>();
        context.Services.AddSingleton<BlockChainDataProvider, BlockChainDataProvider>();
    }
}