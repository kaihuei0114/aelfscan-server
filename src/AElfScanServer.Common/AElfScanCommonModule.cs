using AElf.EntityMapping.Options;
using AElfScanServer.Common.Address.Provider;
using AElfScanServer.Common.Contract.Provider;
using AElfScanServer.Common.Core;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.GraphQL;
using AElfScanServer.Common.HttpClient;
using AElfScanServer.Common.IndexerPluginProvider;
using AElfScanServer.Common.Options;
using AElfScanServer.Common.ThirdPart.Exchange;
using AElfScanServer.Common.Token.Provider;
using AutoResponseWrapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Volo.Abp;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AbpCachingStackExchangeRedisModule)
)]
public class AElfScanCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<ChainOptions>(configuration.GetSection("ChainOptions"));
        Configure<ApiClientOption>(configuration.GetSection("ApiClient"));
        Configure<IndexerOptions>(configuration.GetSection("Indexers"));
        Configure<ExchangeOptions>(configuration.GetSection("Exchange"));
        Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
        Configure<TokenInfoOptions>(configuration.GetSection("TokenInfoOptions"));
        Configure<AssetsInfoOptions>(configuration.GetSection("AssetsInfoOptions"));
        context.Services.AddSingleton<IHttpProvider, HttpProvider>();
        context.Services.AddSingleton<IGraphQlFactory, GraphQlFactory>();
        context.Services.AddTransient<IExchangeProvider, OkxProvider>();
        context.Services.AddTransient<IExchangeProvider, BinanceProvider>();
        context.Services.AddTransient<IExchangeProvider, CoinGeckoProvider>();
        context.Services.AddTransient<ITokenExchangeProvider, TokenExchangeProvider>();
        context.Services.AddTransient<ITokenInfoProvider, TokenInfoProvider>();
        context.Services.AddTransient<IAddressInfoProvider, AddressInfoProvider>();
        context.Services.AddTransient<IGenesisPluginProvider, GenesisPluginProvider>();
        context.Services.AddTransient<ITokenIndexerProvider, TokenIndexerProvider>();
        context.Services.AddTransient<INftCollectionHolderProvider, NftCollectionHolderProvider>();
        context.Services.AddTransient<INftInfoProvider, NftInfoProvider>();
        context.Services.AddTransient<ITokenInfoProvider, TokenInfoProvider>();

        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(NodeBlockProduceIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(RoundIndex)); });

        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyBlockProduceCountIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyBlockProduceDurationIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyCycleCountIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(HourNodeBlockProduceIndex)); });

        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyBlockRewardIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyAvgBlockSizeIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyAvgTransactionFeeIndex)); });


        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyTotalBurntIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyDeployContractIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(ElfPriceIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyTransactionCountIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyUniqueAddressCountIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyActiveAddressCountIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyAvgBlockSizeIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(BlockSizeErrInfoIndex)); });

        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(DailyTransactionRecordIndex)); });
        Configure<CollectionCreateOptions>(x => { x.AddModule(typeof(AddressIndex)); });

        context.Services.AddHttpClient();
        context.Services.AddAutoResponseWrapper();

        AddOpenTelemetry(context);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }

    private void AddOpenTelemetry(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.OnRegistred(options =>
        {
            if (options.ImplementationType.IsDefined(typeof(UmpAttribute), true))
            {
                options.Interceptors.TryAdd<UmpInterceptor>();
            }
        });
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource("AElf")
                    .SetSampler(new AlwaysOnSampler())
                    ;
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter("AElf");
                builder.AddPrometheusExporter();
            });
    }
}