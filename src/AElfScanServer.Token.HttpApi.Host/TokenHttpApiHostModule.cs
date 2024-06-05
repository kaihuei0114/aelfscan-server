using System.Reflection;
using AElf.Indexing.Elasticsearch;
using AElfScanServer.BlockChain;
using AElfScanServer.GraphQL;
using AElfScanServer.Options;
using AElfScanServer.Plugins.Core.Plugins;
using AElfScanServer.TokenDataFunction;
using AElfScanServer.TokenDataFunction.Options;
using AElfScanServer.TokenDataFunction.Provider;
using AElfScanServer.TokenDataFunction.Service;
using AElfScanServer.TokenDataFunction.Worker;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AElfScanServer.Token.HttpApi.Host;

[DependsOn(
    typeof(TokenHttpApiModule),
    typeof(AElfScanServerBlockChainModule),
    typeof(AElfScanCommonModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AElfIndexingElasticsearchModule)
)]
public class TokenHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<WorkerOptions>(configuration.GetSection("Worker"));
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "TokenDataFunctionServer:"; });
        context.Services.AddSingleton<ITokenHolderPercentProvider, TokenHolderPercentProvider>();
        context.Services.AddSingleton<ITokenIndexerProvider, TokenIndexerProvider>();
        context.Services.AddSingleton<ITokenPriceProvider, TokenPriceProvider>();
        context.Services.AddSingleton<INftCollectionHolderProvider, NftCollectionHolderProvider>();
        context.Services.AddTransient<ITokenService, TokenService>();
        context.Services.AddTransient<INftService, NftService>();
        ConfigureGraphQl(context, configuration);
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(TokenHttpApiModule).Assembly);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        context.AddBackgroundWorkerAsync<TokenHolderPercentWorker>();
        app.UseHttpsRedirection();
        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
        var env = context.GetEnvironment();
        Configure(app,env);
    }

    private void ConfigureGraphQl(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddSingleton<IGraphQlFactory, GraphQlFactory>();
        Configure<IndexerOptions>(configuration.GetSection("Indexer"));
    }
    public void ConfigureServices(IServiceCollection services)
    {   
        var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        var pluginAssemblies = Directory.GetFiles(pluginsPath, "*.dll");
        foreach (var pluginAssembly in pluginAssemblies)
        {   
            var assembly = Assembly.LoadFrom(pluginAssembly);
            var pluginTypes = assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);
            foreach (var pluginType in pluginTypes)
            {var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                plugin.ConfigureServices(services);
            }
        }
    }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {   
        var pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        var pluginAssemblies = Directory.GetFiles(pluginsPath, "*.dll");
        foreach (var pluginAssembly in pluginAssemblies)
        {
            var assembly = Assembly.LoadFrom(pluginAssembly);
            var pluginTypes = assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);
            foreach (var pluginType in pluginTypes)
            {   
                var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                plugin.Configure(app, env);
            }
        }
    }
    
    
}