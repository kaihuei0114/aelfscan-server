using AElfScanServer.Address.Provider;
using AElfScanServer.Contract.Provider;
using AElfScanServer.Core;
using AElfScanServer.GraphQL;
using AElfScanServer.HttpClient;
using AElfScanServer.Options;
using AElfScanServer.ThirdPart.Exchange;
using AElfScanServer.Token.Provider;
using AutoResponseWrapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Volo.Abp;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AElfScanServer;

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
        context.Services.AddTransient<IContractProvider, ContractProvider>();

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