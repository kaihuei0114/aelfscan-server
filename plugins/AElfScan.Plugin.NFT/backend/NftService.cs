using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Dtos;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace NFT.backend;

public interface INftService
{
   
    Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol);
 
}

public class NftService : INftService, ISingletonDependency
{
    private const int MaxResultCount = 1000;
    private readonly ILogger<NftService> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptionsMonitor;

    
    public NftService(ITokenIndexerProvider tokenIndexerProvider, ILogger<NftService> logger,
        IObjectMapper objectMapper, 
         ITokenPriceService tokenPriceService, 
         IOptionsMonitor<TokenInfoOptions> tokenInfoOptionsMonitor, 
        ITokenInfoProvider tokenInfoProvider)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _tokenPriceService = tokenPriceService;
        _tokenInfoOptionsMonitor = tokenInfoOptionsMonitor;
        _tokenInfoProvider = tokenInfoProvider;
    }
    
   
    
    public async Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol)
    {
        var nftItems = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, symbol);
     //   AssertHelper.NotEmpty(nftItems, "this nft item not exist");
        var nftItem = nftItems[0];
        //get collection info
        var collectionInfos = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, nftItem.CollectionSymbol);
     //   AssertHelper.NotEmpty(collectionInfos, "this nft collection not exist");
        var collectionInfo = collectionInfos[0];
        var nftItemDetailDto = _objectMapper.Map<IndexerTokenInfoDto, NftItemDetailDto>(nftItem);
        nftItemDetailDto.Quantity = DecimalHelper.Divide(nftItem.TotalSupply, nftItem.Decimals);
        nftItemDetailDto.Item.ImageUrl = TokenInfoHelper.GetImageUrl(nftItem.ExternalInfo,
            () => _tokenInfoProvider.BuildImageUrl(nftItem.Symbol));
        var marketInfo = _tokenInfoOptionsMonitor.CurrentValue.GetMarketInfo(CommonConstant.DefaultMarket);
        marketInfo.MarketUrl = string.Format(marketInfo.MarketUrl, symbol);
        nftItemDetailDto.MarketPlaces = marketInfo;
        nftItemDetailDto.NftCollection = new TokenBaseInfo
        {
            Name = collectionInfo.TokenName,
            Symbol = collectionInfo.Symbol,
            Decimals = collectionInfo.Decimals, 
            ImageUrl = TokenInfoHelper.GetImageUrl(collectionInfo.ExternalInfo,
                    () => _tokenInfoProvider.BuildImageUrl(collectionInfo.Symbol))
        };
        return nftItemDetailDto;
    }

    
   
    
   

    
}