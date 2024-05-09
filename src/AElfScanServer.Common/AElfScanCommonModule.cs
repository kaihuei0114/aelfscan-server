using AutoResponseWrapper;
using AElfScanServer.GraphQL;
using AElfScanServer.HttpClient;
using AElfScanServer.Options;
using AElfScanServer.ThirdPart.Exchange;
using AElfScanServer.Token.Provider;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer;

[DependsOn(
    typeof(AbpAutoMapperModule)
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

        context.Services.AddHttpClient();
        context.Services.AddAutoResponseWrapper();
    }
}