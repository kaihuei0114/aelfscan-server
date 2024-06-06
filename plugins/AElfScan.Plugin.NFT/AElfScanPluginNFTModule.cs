using AElfScanServer;
using AElfScanServer.Token;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace NFT;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AElfScanServerTokenModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AElfScanCommonModule)
)]
public class AElfScanPluginNFTModule : AElfScanPluginBaseModule<AElfScanPluginNFTModule>
{
    protected override string Name { get; }
    protected override string Version { get; }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}