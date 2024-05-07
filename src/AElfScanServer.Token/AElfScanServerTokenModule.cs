using AElfScanServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElfScanServer.Token;

public class AElfScanServerTokenModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<TokenServerOption>(context.Services.GetConfiguration().GetSection("TokenServer"));
        context.Services.AddSingleton<ITokenProvider, TokenProvider>();
    }
}