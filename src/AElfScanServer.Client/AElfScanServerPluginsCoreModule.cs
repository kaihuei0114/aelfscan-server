

using AElf.Indexing.Elasticsearch;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Plugins.Core;
[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AElfIndexingElasticsearchModule)
)]
public class AElfScanServerPluginsCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
       
    }
}