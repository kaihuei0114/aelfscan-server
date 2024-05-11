using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Token.Provider;

public interface ITokenInfoProvider
{
    TokenBaseInfo OfTokenBaseInfo(IndexerTokenInfoDto tokenInfo);
    
    string BuildImageUrl(string symbol, bool useAssetUrl = false);
    
    Task<List<TransactionFeeDto>> ConvertTransactionFeeAsync(Dictionary<string, TokenPriceDto> priceDict, List<ExternalInfoDto> externalInfos);
}

public class TokenInfoProvider : ITokenInfoProvider, ISingletonDependency
{
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptionsMonitor;
    private readonly IOptionsMonitor<AssetsInfoOptions> _assetsInfoOptionsMonitor;
    private readonly ITokenPriceService _tokenPriceService;
    public TokenInfoProvider(IOptionsMonitor<TokenInfoOptions> tokenInfoOptions,
        IOptionsMonitor<AssetsInfoOptions> assetsInfoOptions, ITokenPriceService tokenPriceService)
    {
        _tokenInfoOptionsMonitor = tokenInfoOptions;
        _assetsInfoOptionsMonitor = assetsInfoOptions;
        _tokenPriceService = tokenPriceService;
    }

    public TokenBaseInfo OfTokenBaseInfo(IndexerTokenInfoDto tokenInfo)
    {
        return new TokenBaseInfo
        {
            Name = tokenInfo.TokenName,
            Symbol = tokenInfo.Symbol,
            Decimals = tokenInfo.Decimals,
            ImageUrl = TokenInfoHelper.GetImageUrl(tokenInfo.ExternalInfo,
                () => BuildImageUrl(tokenInfo.Symbol))
        };
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

    public async Task<List<TransactionFeeDto>> ConvertTransactionFeeAsync(Dictionary<string, TokenPriceDto> priceDict, List<ExternalInfoDto> externalInfos)
    {
        var feeDtos = TokenInfoHelper.GetTransactionFee(externalInfos);
        foreach (var transactionFeeDto in feeDtos)
        {
            if (!priceDict.TryGetValue(transactionFeeDto.Symbol, out var priceDto))
            {
                priceDto = await _tokenPriceService.GetTokenPriceAsync(transactionFeeDto.Symbol,
                    CurrencyConstant.UsdCurrency);
                priceDict[transactionFeeDto.Symbol] = priceDto;
            }
            transactionFeeDto.AmountOfUsd = Math.Round(transactionFeeDto.Amount * priceDto.Price,
                CommonConstant.UsdValueDecimals);
        }

        return feeDtos;
    }
}