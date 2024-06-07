using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common.BFF.Core;

[DependsOn(
    typeof(AbpAutoMapperModule)
)]
public class AElfScanServerBffCoreModule : AbpModule
{
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AElfScanServerBffCoreModule>(); });
    }
}