using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Contracts.MultiToken;
using AElfScanServer.Common.Constant;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using GetTokenInfoInput = AElf.Client.MultiToken.GetTokenInfoInput;

namespace AElfScanServer.Common.Token.Provider;

public interface ITokenInfoProvider
{
    TokenBaseInfo OfTokenBaseInfo(IndexerTokenInfoDto tokenInfo);

    string BuildImageUrl(string symbol, bool useAssetUrl = false);
    Task<string> GetTokenImageAsync(string symbol);

    Task<List<TransactionFeeDto>> ConvertTransactionFeeAsync(Dictionary<string, CommonTokenPriceDto> priceDict,
        List<ExternalInfoDto> externalInfos);
}

public class TokenInfoProvider : ITokenInfoProvider, ISingletonDependency
{
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptionsMonitor;
    private readonly IOptionsMonitor<AssetsInfoOptions> _assetsInfoOptionsMonitor;
    private readonly ITokenPriceService _tokenPriceService;
    private Dictionary<string, string> _tokenImageUrlCache;
    private readonly GlobalOptions _globalOptions;
    private ILogger<TokenInfoProvider> _logger;

    public TokenInfoProvider(IOptionsMonitor<TokenInfoOptions> tokenInfoOptions,
        IOptionsMonitor<AssetsInfoOptions> assetsInfoOptions, IOptionsMonitor<GlobalOptions> globalOptions,
        ITokenPriceService tokenPriceService, ILogger<TokenInfoProvider> logger)
    {
        _tokenInfoOptionsMonitor = tokenInfoOptions;
        _assetsInfoOptionsMonitor = assetsInfoOptions;
        _tokenPriceService = tokenPriceService;
        _tokenImageUrlCache = new Dictionary<string, string>();

        _globalOptions = globalOptions.CurrentValue;
        _logger = logger;
    }

    public TokenBaseInfo OfTokenBaseInfo(IndexerTokenInfoDto tokenInfo)
    {
        return new TokenBaseInfo
        {
            Name = tokenInfo.TokenName,
            Symbol = tokenInfo.Symbol,
            Decimals = tokenInfo.Decimals,
            ImageUrl = GetTokenImageAsync(tokenInfo.Symbol).Result
        };
    }

    public async Task<string> GetTokenImageAsync(string symbol)
    {
        try
        {
            if (_tokenImageUrlCache.TryGetValue(symbol, out var imageBase64))
            {
                return imageBase64;
            }


            AElfClient elfClient = new AElfClient(_globalOptions.ChainNodeHosts["AELF"]);
            var tokenInfoInput = new GetTokenInfoInput
            {
                Symbol = symbol
            };
            var transactionGetToken =
                await elfClient.GenerateTransactionAsync(
                    elfClient.GetAddressFromPrivateKey(GlobalOptions.PrivateKey),
                    "JRmBduh4nXWi1aXgdUsj5gJrzeZb2LxmrAbf7W99faZSvoAaE",
                    "GetTokenInfo",
                    tokenInfoInput);
            var txWithSignGetToken = elfClient.SignTransaction(GlobalOptions.PrivateKey, transactionGetToken);
            var transactionGetTokenResult = await elfClient.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = txWithSignGetToken.ToByteArray().ToHex()
            });

            var token = new TokenInfo();
            token.MergeFrom(ByteArrayHelper.HexStringToByteArray(transactionGetTokenResult));

            if (token.ExternalInfo.Value.TryGetValue("__ft_image_uri", out var url))
            {
                _tokenImageUrlCache.Add(symbol, url);
                return url;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("get token:{0} image base64  error:{1}", symbol, e);
        }


        return "";
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

    public async Task<List<TransactionFeeDto>> ConvertTransactionFeeAsync(
        Dictionary<string, CommonTokenPriceDto> priceDict, List<ExternalInfoDto> externalInfos)
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