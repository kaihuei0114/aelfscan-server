using System;
using System.Collections.Generic;
using System.Linq;
using AElf.EntityMapping.Elasticsearch;
using AElfScanServer.Common.BlockChain.HttpApi;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common.BlockChainDataFunction;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AElfEntityMappingElasticsearchModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(BlockChainHttpApiModule),
    typeof(AElfScanCommonModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule)
)]
public class BlockChainHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "BlockChainDataFunctionServer:"; });
        ConfigureGraphQl(context, configuration);
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<BlockChainHttpApiHostModule>(); });
       

        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(BlockChainHttpApiHostModule).Assembly);
        });
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseHttpsRedirection();
        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }


    private void ConfigureGraphQl(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }
}