using AElfScanServer.BFF.Core;
using AElfScanServer.BFF.Core.Adaptor;
using AElfScanServer.BFF.Core.Options;
using AElfScanServer.BFF.Core.Provider;
using AElfScanServer.BFF.Core.RequestHandler;
using AElfScanServer.BFF.Core.SchemaManager;
using Volo.Abp;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common.BFF;

[DependsOn(
    typeof(AElfScanServerBffCoreModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public class AElfScanServerBffModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<SchemaOption>(configuration.GetSection("Schema"));
        Configure<AwsS3Option>(configuration.GetSection("AwsS3")); 
        //context.Services.AddTransient<IHttpRequestProvider, HttpRequestProvider>();
        context.Services.AddSingleton<ISchemaManager, SchemaManager>();
        context.Services.AddSingleton<IGraphQlExecutorProvider, GraphQlExecutorProvider>();
        context.Services.AddSingleton<IAwsS3Provider, AwsS3Provider>();
        context.Services.AddSingleton<IHttpRequestProvider, HttpRequestProvider>();
        //context.Services.AddScoped<HttpDirectiveMiddleware>();
        context.Services.AddScoped<FromJsonDirectiveMiddleware>();
        context.Services.AddGraphQLServer();
        context.Services.AddHttpClient();
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseMiddleware<CustomHttpPostMiddlewareBase>();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGraphQL();
        });
    }
}