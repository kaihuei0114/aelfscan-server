using AElfScanServer.Address.HttpApi.AppServices;
using AElfScanServer.Address.HttpApi.Options;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.BlockChain;
using AElfScanServer.Token;
using AElfScanServer;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Provider;
using AElfScanServer.TokenDataFunction.Service;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Address.HttpApi;

[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(AElfScanCommonModule),
    typeof(AElfScanServerTokenModule),
typeof(AElfScanServerBlockChainModule)
    )]

public class AElfScanServerAddressHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<AElfScanServerAddressHttpApiModule>(); });
        context.Services.AddSingleton<IIndexerGenesisProvider, IndexerGenesisProvider>();
        context.Services.AddTransient<IAddressAppService, AddressAppService>();
        context.Services.AddTransient<ITokenService, TokenService>();
        context.Services.AddSingleton<IIndexerTokenProvider, IndexerTokenProvider>();
        context.Services.AddTransient<IContractAppService, ContractAppService>();
        context.Services.AddSingleton<IDecompilerProvider, DecompilerProvider>();
        context.Services.AddSingleton<ITokenIndexerProvider, TokenIndexerProvider>();
        
        var configuration = context.Services.GetConfiguration();
        
        Configure<BlockChainOptions>(configuration.GetSection("BlockChain"));
    }
}