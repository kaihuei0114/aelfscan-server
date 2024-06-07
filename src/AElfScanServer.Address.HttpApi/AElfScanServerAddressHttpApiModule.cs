using AElfScanServer.Address.HttpApi.AppServices;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.BlockChain;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.Token.HttpApi.Provider;
using AElfScanServer.Token.HttpApi.Service;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace AElfScanServer.Common.Address.HttpApi;

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
        context.Services.AddSingleton<INftInfoProvider, NftInfoProvider>();
        context.Services.AddSingleton<ITokenAssetProvider, TokenAssetProvider>();
        context.Services.AddSingleton<ITokenPriceService, TokenPriceService>();

        var configuration = context.Services.GetConfiguration();
        
        Configure<GlobalOptions>(configuration.GetSection("BlockChain"));
    }
}