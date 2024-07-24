using AElfScanServer.Common;
using AElfScanServer.Grains.Grain.Ads;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElfScanServer.Grains;

[DependsOn(typeof(AElfScanCommonModule))]
public class AElfScanServerGrainsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();


        context.Services.AddSingleton<IAdsGrain, AdsGain>();
    }
}