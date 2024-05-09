using System;
using AElfScanServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Token.Provider;

public interface ITokenImageProvider
{
    string BuildImageUrl(string symbol, bool useAssetUrl = false);
}

public class TokenImageProvider : ITokenImageProvider, ISingletonDependency
{
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptionsMonitor;
    private readonly IOptionsMonitor<AssetsInfoOptions> _assetsInfoOptionsMonitor;

    public TokenImageProvider(IOptionsMonitor<TokenInfoOptions> tokenInfoOptions,
        IOptionsMonitor<AssetsInfoOptions> assetsInfoOptions)
    {
        _tokenInfoOptionsMonitor = tokenInfoOptions;
        _assetsInfoOptionsMonitor = assetsInfoOptions;
    }

    public string BuildImageUrl(string symbol, bool useAssetUrl = false)
    {
        if (symbol.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        if (_tokenInfoOptionsMonitor.CurrentValue.TokenInfos.TryGetValue(symbol, out var info))
        {
            return info.ImageUrl;
        }

        if (_assetsInfoOptionsMonitor.CurrentValue.IsEmpty())
        {
            return string.Empty;
        }

        return useAssetUrl ? _assetsInfoOptionsMonitor.CurrentValue.BuildImageUrl(symbol) : string.Empty;
    }
}