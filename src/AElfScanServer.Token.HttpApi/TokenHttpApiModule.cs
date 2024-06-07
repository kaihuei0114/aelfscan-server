using AElfScanServer.Token;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common.Token.HttpApi;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AElfScanServerTokenModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class TokenHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<TokenHttpApiModule>(); });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        base.OnApplicationInitialization(context);
    }
}