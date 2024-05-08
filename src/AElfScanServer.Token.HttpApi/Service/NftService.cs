using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Token.Constant;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.TokenDataFunction.Dtos;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Provider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using NftInfoDto = AElfScanServer.Token.Dtos.NftInfoDto;
using SymbolType = AElfScanServer.Dtos.SymbolType;
using TokenPriceDto = AElfScanServer.Dtos.TokenPriceDto;

namespace AElfScanServer.TokenDataFunction.Service;

public interface INftService
{
    public Task<ListResponseDto<NftInfoDto>> GetNftCollectionListAsync(TokenListInput input);
    public Task<NftDetailDto> GetNftCollectionDetailAsync(string chainId, string collectionSymbol);
    public Task<NftTransferInfosDto> GetNftCollectionTransferInfosAsync(TokenTransferInput input);
    public Task<ListResponseDto<TokenHolderInfoDto>> GetNftCollectionHolderInfosAsync(TokenHolderInput input);
    public Task<ListResponseDto<NftHolderInfoDto>> GetNftCollectionHolderInfoAsync(NftHolderInfoInput input);
    public Task<NftInventorysDto> GetNftCollectionInventoryAsync(NftInventoryInput input);
    Task<NftInfoListDto> GetAddressTokenListAsync(GetNftListInput input);
    Task<TokenTransferInfoListDto> GetAddressTransfersAsync(GetTransferInfoListInput input);
    Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol);
    Task<ListResponseDto<NftItemActivityDto>> GetNftItemActivityAsync(NftItemActivityInput input);
    Task<ListResponseDto<NftItemHolderInfoDto>> GetNftItemHoldersAsync(NftItemHolderInfoInput input);
    Task<(decimal, string)> GetNftFloorPriceAsync(string chainId, string symbol);
}

public class NftService : INftService, ISingletonDependency
{
    private const int MaxPageSize = 1000;
    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly IBlockChainProvider _blockChainProvider;
    private readonly ILogger<NftService> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly INftCollectionHolderProvider _collectionHolderProvider;
    private readonly INftInfoProvider _nftInfoProvider;
    private readonly ITokenPriceService _tokenPriceService;
    
    public NftService(ITokenIndexerProvider tokenIndexerProvider, ILogger<NftService> logger,
        IObjectMapper objectMapper, IBlockChainProvider blockChainProvider,
        INftCollectionHolderProvider collectionHolderProvider, INftInfoProvider nftInfoProvider, ITokenPriceService tokenPriceService, IOptionsMonitor<ChainOptions> chainOptions)
    {
        _tokenIndexerProvider = tokenIndexerProvider;
        _logger = logger;
        _objectMapper = objectMapper;
        _blockChainProvider = blockChainProvider;
        _collectionHolderProvider = collectionHolderProvider;
        _nftInfoProvider = nftInfoProvider;
        _tokenPriceService = tokenPriceService;
        _chainOptions = chainOptions;
    }


    public async Task<ListResponseDto<NftInfoDto>> GetNftCollectionListAsync(TokenListInput input)
    {
        input.SetDefaultSort();
        input.Types = new List<SymbolType> { SymbolType.Nft_Collection };
        var indexerNftListDto = await _tokenIndexerProvider.GetTokenListAsync(input);
        if (indexerNftListDto.Items.IsNullOrEmpty())
        {
            return new ListResponseDto<NftInfoDto>();
        }
        //get collection supply
        var collectionSymbols = indexerNftListDto.Items.Select(o => o.Symbol).ToList();
        var groupAndSumSupply = await GetCollectionSupplyAsync(input.ChainId, collectionSymbols);
        var list = indexerNftListDto.Items.Select(item =>
        {
            var nftInfoDto = _objectMapper.Map<IndexerTokenInfoDto, NftInfoDto>(item);
            //convert url
            nftInfoDto.Items = groupAndSumSupply.TryGetValue(item.Symbol, out var sumSupply) ? sumSupply : 0;
            return nftInfoDto;
        }).ToList();
        return new ListResponseDto<NftInfoDto>
        {
            Total = indexerNftListDto.TotalCount,
            List = list
        };
    }

    public async Task<NftDetailDto> GetNftCollectionDetailAsync(string chainId, string collectionSymbol)
    {
        var getCollectionInfoTask = _tokenIndexerProvider.GetTokenDetailAsync(chainId, collectionSymbol);
        var getFloorPricePairTask = GetNftFloorPriceAsync(chainId, collectionSymbol);
        var collectionSymbols = new List<string> { collectionSymbol };
        var groupAndSumSupplyTask = GetCollectionSupplyAsync(chainId, collectionSymbols);

        await Task.WhenAll(getCollectionInfoTask, getFloorPricePairTask, groupAndSumSupplyTask);

        var collectionInfoDtos = await getCollectionInfoTask;
        AssertHelper.NotEmpty(collectionInfoDtos, "this nft not exist");
        var collectionInfo = collectionInfoDtos[0];
        var nftDetailDto = _objectMapper.Map<IndexerTokenInfoDto, NftDetailDto>(collectionInfo);
        nftDetailDto.TokenContractAddress = _chainOptions.CurrentValue.GetChainInfo(chainId)?.TokenContractAddress;
        nftDetailDto.Items = (await groupAndSumSupplyTask).TryGetValue(collectionInfo.Symbol, out var sumSupply) ? sumSupply : 0;
        //of floor price
        var floorPricePair = await getFloorPricePairTask;
        if (floorPricePair.Item2.IsNullOrEmpty())
        {
            return nftDetailDto;
        }
        var priceDto = await _tokenPriceService.GetTokenPriceAsync(floorPricePair.Item2, CurrencyConstant.UsdCurrency);
        nftDetailDto.FloorPrice = floorPricePair.Item1;
        nftDetailDto.FloorPriceOfUsd = Math.Round(floorPricePair.Item1 * priceDto.Price, CommonConstant.UsdValueDecimals);
        return nftDetailDto;
    }

    public async Task<NftTransferInfosDto> GetNftCollectionTransferInfosAsync(TokenTransferInput input)
    {
        input.Types = new List<SymbolType> { SymbolType.Nft };
        
        var indexerNftTransfer = await _tokenIndexerProvider.GetTokenTransferInfoAsync(input);

        var list = await ConvertIndexerNftTransferDtoAsync(indexerNftTransfer.Items, input.ChainId);

        var result = new NftTransferInfosDto
        {
            Total = indexerNftTransfer.TotalCount,
            List = list
        };
        if (input.IsSearchAddress())
        {
            result.IsAddress = true;
            result.Items = await _tokenIndexerProvider.GetHolderInfoAsync(SymbolType.Nft, input.ChainId, input.Search);
        }
        return result;
    }

    public async Task<ListResponseDto<TokenHolderInfoDto>> GetNftCollectionHolderInfosAsync(TokenHolderInput input)
    {
        input.SetDefaultSort();
        input.Types = new List<SymbolType> { SymbolType.Nft };

        var indexerTokenHolderInfo = await _tokenIndexerProvider.GetTokenHolderInfoAsync(input);

        var list = await ConvertIndexerNftHolderInfoDtoAsync(indexerTokenHolderInfo.Items, input.ChainId, input.CollectionSymbol);

        return new ListResponseDto<TokenHolderInfoDto>
        {
            Total = indexerTokenHolderInfo.TotalCount,
            List = list
        };    
    }

    public async Task<ListResponseDto<NftHolderInfoDto>> GetNftCollectionHolderInfoAsync(NftHolderInfoInput input)
    {
        var holderInfos =
            await _collectionHolderProvider.GetNftCollectionHolderInfoAsync(input.CollectionSymbol, input.ChainId);

        //todo query by collection symbol
        var nftList = await _tokenIndexerProvider.GetTokenDetailAsync(input.ChainId, input.CollectionSymbol);

        AssertHelper.NotEmpty(nftList, "this nft not exist");

        var supply = nftList[0].Supply;
        var addressInput = new AElfAddressInput
        {
            Addresses = holderInfos.Select(dto => dto.Address).Distinct().ToList(),
            ChainId = input.ChainId
        };

        var addressDic = await _blockChainProvider.GetAddressDictionaryAsync(addressInput);
        var list = new List<NftHolderInfoDto>();
        foreach (var nftCollectionHolderInfoIndex in holderInfos)
        {
            var nftHolderInfoDto = new NftHolderInfoDto()
            {
                Quantity = nftCollectionHolderInfoIndex.FormatQuantity,
                Percentage = nftCollectionHolderInfoIndex.FormatQuantity / supply
            };
            if (addressDic.TryGetValue(nftCollectionHolderInfoIndex.Address, out var address))
            {
                nftHolderInfoDto.Address = address;
            }

            list.Add(nftHolderInfoDto);
        }

        return new ListResponseDto<NftHolderInfoDto>()
        {
            Total = holderInfos.Count,
            List = list
        };
    }

    public async Task<NftInventorysDto> GetNftCollectionInventoryAsync(NftInventoryInput input)
    {
        var tokenHolderInput = new TokenHolderInput
        {
            ChainId = input.ChainId, Types = new List<SymbolType> { SymbolType.Nft },
            CollectionSymbol = input.CollectionSymbol, Search = input.Search
        };
        var indexerNftHolder = await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);
        var list = await ConvertIndexerNftInventoryDtoAsync(indexerNftHolder.Items, input.ChainId);
        var result = new NftInventorysDto
        {
            Total = indexerNftHolder.TotalCount, List = list
        };
        if (input.IsSearchAddress())
        {
            result.IsAddress = true;
            result.Items = indexerNftHolder.Items.Select(i => new HolderInfo
            {
                Balance = i.FormatAmount, Symbol = i.Token.Symbol
            }).ToList();
        }
        return result;
    }

    public async Task<NftInfoListDto> GetAddressTokenListAsync(GetNftListInput input)
    {
        var tokenHolderInput = _objectMapper.Map<GetNftListInput, TokenHolderInput>(input);

        var indexerAddressTokens = await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);

        var list = await ConvertAddressNftAsync(indexerAddressTokens.Items, input.ChainId);

        return new NftInfoListDto
        {
            Total = indexerAddressTokens.TotalCount,
            List = list
        };
    }

    public async Task<TokenTransferInfoListDto> GetAddressTransfersAsync(GetTransferInfoListInput input)
    {
        var tokenTransferInput = _objectMapper.Map<GetTransferInfoListInput, TokenTransferInput>(input);

        var indexerTokenTransfer = await _tokenIndexerProvider.GetTokenTransferInfoAsync(tokenTransferInput);

        var list = await ConvertAddressTransferAsync(indexerTokenTransfer.Items, input.ChainId, input.Address);

        return new TokenTransferInfoListDto
        {
            AssetInUsd = 0,
            Total = indexerTokenTransfer.TotalCount,
            List = list
        };
    }

    public async Task<NftItemDetailDto> GetNftItemDetailAsync(string chainId, string symbol)
    {
        var nftItems = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, symbol);
        AssertHelper.NotEmpty(nftItems, "this nft item not exist");
        var nftItem = nftItems[0];
        //get collection info
        var collectionInfos = await _tokenIndexerProvider.GetTokenDetailAsync(chainId, nftItem.CollectionSymbol);
        AssertHelper.NotEmpty(collectionInfos, "this nft collection not exist");
        var collectionInfo = collectionInfos[0];
        var nftItemDetailDto = _objectMapper.Map<IndexerTokenInfoDto, NftItemDetailDto>(nftItem);
        nftItemDetailDto.Quantity = DecimalHelper.DivideLong(nftItem.TotalSupply, nftItem.Decimals);
        nftItemDetailDto.NftCollection = new TokenBaseInfo
        {
            Name = collectionInfo.TokenName,
            Symbol = collectionInfo.Symbol,
            Decimals = collectionInfo.Decimals, 
            //ImageUrl = collectionInfo.ExternalInfo;
        };
        return nftItemDetailDto;
    }

    public async Task<ListResponseDto<NftItemActivityDto>> GetNftItemActivityAsync(NftItemActivityInput input)
    {
        var list = await ConvertNftItemActivityAsync(input.ChainId);

        return new ListResponseDto<NftItemActivityDto>
        {
            Total = list.Count,
            List = list
        };
    }

    public async Task<ListResponseDto<NftItemHolderInfoDto>> GetNftItemHoldersAsync(NftItemHolderInfoInput input)
    {
        var tokenHolderInput = _objectMapper.Map<NftItemHolderInfoInput, TokenHolderInput>(input);
        tokenHolderInput.SetDefaultSort();
        var tokenHolderInfoTask = _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);
        var tokenDetailTask = _tokenIndexerProvider.GetTokenDetailAsync(input.ChainId, input.Symbol);
        await Task.WhenAll(tokenHolderInfoTask, tokenDetailTask);
        var nftItemHolderInfos = await tokenHolderInfoTask;
        var nftItemList = await tokenDetailTask;
        AssertHelper.NotEmpty(nftItemList, "this nft not exist");
        
        var supply = nftItemList[0].Supply;

        var list = new List<NftItemHolderInfoDto>();
        foreach (var nftCollectionHolderInfoIndex in nftItemHolderInfos.Items)
        {
            var nftItemHolderInfoDto = new NftItemHolderInfoDto()
            {
                Quantity = nftCollectionHolderInfoIndex.FormatAmount
            };
            nftItemHolderInfoDto.Address = new CommonAddressDto
            {
                Address = nftCollectionHolderInfoIndex.Address
            };

            if (supply > 0)
            {
                nftItemHolderInfoDto.Percentage =
                    Math.Round((decimal)nftCollectionHolderInfoIndex.Amount / supply * 100, CommonConstant.PercentageValueDecimals);
            }

            list.Add(nftItemHolderInfoDto);
        }

        return new ListResponseDto<NftItemHolderInfoDto>()
        {
            Total = nftItemHolderInfos.TotalCount,
            List = list
        };
    }

    public async Task<(decimal, string)> GetNftFloorPriceAsync(string chainId, string symbol)
    {
        try
        {
            var nftListingsDto = new GetNFTListingsDto()
            {
                ChainId = chainId,
                Symbol = symbol,
                SkipCount = 0,
                MaxResultCount = 1
            };

            var listingDto = await _nftInfoProvider.GetNftListingsAsync(nftListingsDto);
            if (listingDto.Items.IsNullOrEmpty())
            {
                return (0, null);
            }

            var item = listingDto.Items[0];
            var tokenPrice = item.Prices;
            return (tokenPrice, item.PurchaseToken.Symbol);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[GetNftFloorPrice] error.");
            return (0, null);
        }
    }

    private async Task<List<NftItemActivityDto>> ConvertNftItemActivityAsync(string chainId)
    {
        var list = new List<NftItemActivityDto>();

        for (int i = 2; i > 0; i--)
        {
            var activityDto = new NftItemActivityDto()
            {
                Action = "Sale",
                From = new CommonAddressDto() { Address = "CeQt2cD4rG3Un1QW6FAzGJcdGYoyE987dE7Gr816mWDQw1HRN" },
                To = new CommonAddressDto() { Address = "2jwoGHSPUWuCu49eCgnmEy9qewT4APEA1eoYzg5WbU3Mm2nzt" },
                Price = new decimal(i * 1.2),
                PriceSymbol = "ELF",
                Quantity = i + 10,
                TransactionId = i == 1
                    ? "f0bed7aba6f177818eb18a07cdffd4f0e52aed22bf5fea5c48973c06b7e9bb0d"
                    : "6a52874dddc313c9efb8b633b5318ef0582b7e72c028b0f85754bff463c5d14f",
                BlockHeight = 1,
                BlockTime = 1713619225346 + i    
            };
            list.Add(activityDto);
        }
        return list;
    }

    private async Task<List<AddressNftInfoDto>> ConvertAddressNftAsync(
        List<IndexerTokenHolderInfoDto> indexerAddressTokens, string chainId)
    {
        var symbolList = indexerAddressTokens.SelectMany(dto => new List<string> { dto.Token.Symbol }).Distinct()
            .ToList();
        var tokenDic = await GetTokenDicAsync(symbolList, chainId);

        var list = new List<AddressNftInfoDto>();
        foreach (var indexerAddressToken in indexerAddressTokens)
        {
            var addressTokenDto =
                _objectMapper.Map<IndexerTokenHolderInfoDto, AddressNftInfoDto>(indexerAddressToken);

            if (tokenDic.TryGetValue(indexerAddressToken.Token.Symbol, out var item))
            {
                addressTokenDto.Item = item.Token;
            }

            list.Add(addressTokenDto);
        }

        return list;
    }

    private async Task<List<AddressTokenTransferInfoDto>> ConvertAddressTransferAsync(
        List<IndexerTransferInfoDto> indexerTokenTransfer, string chainId, string address)
    {
        var addressInput = new AElfAddressInput
        {
            Addresses = indexerTokenTransfer.SelectMany(dto => new List<string> { dto.From, dto.To }).Distinct()
                .ToList(),
            ChainId = chainId
        };

        var addressDic = await _blockChainProvider.GetAddressDictionaryAsync(addressInput);

        var symbolList = indexerTokenTransfer.Select(dto => dto.Token.Symbol).Distinct().ToList();
        var tokenDic = await GetTokenDicAsync(symbolList, chainId);

        var list = new List<AddressTokenTransferInfoDto>();
        foreach (var indexerTransferInfoDto in indexerTokenTransfer)
        {
            var addressTokenTransferDto =
                _objectMapper.Map<IndexerTransferInfoDto, AddressTokenTransferInfoDto>(indexerTransferInfoDto);

            if (indexerTransferInfoDto.From != null &&
                addressDic.TryGetValue(indexerTransferInfoDto.From, out var from))
            {
                addressTokenTransferDto.From = from;
            }

            if (indexerTransferInfoDto.To != null && addressDic.TryGetValue(indexerTransferInfoDto.To, out var to))
            {
                addressTokenTransferDto.To = to;
            }

            if (indexerTransferInfoDto.Token?.Symbol != null &&
                tokenDic.TryGetValue(indexerTransferInfoDto.Token.Symbol, out var item))
            {
                addressTokenTransferDto.Asset = item.Token;
            }

            addressTokenTransferDto.TransferStatus = indexerTransferInfoDto.From == address
                ? TransferStatusType.Out
                : TransferStatusType.In;

            list.Add(addressTokenTransferDto);
        }

        return list;
    }

    private async Task<List<NftTransferInfoDto>> ConvertIndexerNftTransferDtoAsync(
        List<IndexerTransferInfoDto> indexerNftTransfer, string chainId)
    {
        var symbolList = indexerNftTransfer.Select(dto => dto.Token.Symbol).Distinct().ToList();
        
        var tokenDic = await GetTokenDicAsync(symbolList, chainId);

        var list = new List<NftTransferInfoDto>();
        foreach (var indexerNftTransferInfoDto in indexerNftTransfer)
        {
            var tokenTransferDto =
                _objectMapper.Map<IndexerTransferInfoDto, NftTransferInfoDto>(indexerNftTransferInfoDto);
            
            if (!indexerNftTransferInfoDto.From.IsNullOrEmpty())
            {
                tokenTransferDto.From = new CommonAddressDto
                {
                    Address = indexerNftTransferInfoDto.From
                };
            }
            if (!indexerNftTransferInfoDto.To.IsNullOrEmpty())
            {
                tokenTransferDto.From = new CommonAddressDto
                {
                    Address = indexerNftTransferInfoDto.To
                };
            }

            if (indexerNftTransferInfoDto.Token?.Symbol != null &&
                tokenDic.TryGetValue(indexerNftTransferInfoDto.Token.Symbol, out var item))
            {
                tokenTransferDto.Item = item.Token;
            }

            list.Add(tokenTransferDto);
        }

        return list;
    }

    private async Task<List<NftInventoryDto>> ConvertIndexerNftInventoryDtoAsync(
        List<IndexerTokenHolderInfoDto> indexerNftHolder, string chainId)
    {
        var list = new List<NftInventoryDto>();
        if (indexerNftHolder.IsNullOrEmpty())
        {
            return list;
        }
        var priceDict = new Dictionary<string, TokenPriceDto>();
        var symbolList = indexerNftHolder.Select(dto => dto.Token.Symbol).Distinct().ToList();
        //batch query symbol last sale info
        var lastSaleInfoDict = new Dictionary<string, NftSalInfoDto>();
        foreach (var indexerTokenHolderInfoDto in indexerNftHolder)
        {
            var nftInventoryDto =
                _objectMapper.Map<IndexerTokenHolderInfoDto, NftInventoryDto>(indexerTokenHolderInfoDto);
            var symbol = nftInventoryDto.Item.Symbol;
            if (lastSaleInfoDict.TryGetValue(symbol, out var lastSaleInfo))
            {
                nftInventoryDto.LastTransactionId = lastSaleInfo.TransactionId;
                nftInventoryDto.BlockHeight = lastSaleInfo.BlockHeight;
                nftInventoryDto.LastSaleAmount = lastSaleInfo.SaleAmount;
                nftInventoryDto.LastSaleAmountSymbol = lastSaleInfo.SaleAmountSymbol;
                if (!priceDict.TryGetValue(lastSaleInfo.SaleAmountSymbol, out var priceDto))
                {
                    priceDto = await _tokenPriceService.GetTokenPriceAsync(lastSaleInfo.SaleAmountSymbol,
                        CurrencyConstant.UsdCurrency);
                    priceDict[lastSaleInfo.SaleAmountSymbol] = priceDto;
                }
                nftInventoryDto.LastSalePriceInUsd = Math.Round(nftInventoryDto.LastSaleAmount * priceDto.Price, CommonConstant.UsdValueDecimals);
            }
            list.Add(nftInventoryDto);
        }
        return list;
    }
    
    private async Task<Dictionary<string, TokenCommonDto>> GetTokenDicAsync(List<string> symbols, string chainId)
    {
        var input = new TokenListInput
        {
            ChainId = chainId,
            Symbols = symbols
        };
        var indexerTokenListDto = await _tokenIndexerProvider.GetTokenListAsync(input);
        var tokenInfoDtoList = _objectMapper.Map<List<IndexerTokenInfoDto>, List<TokenCommonDto>>(indexerTokenListDto.Items);
        return tokenInfoDtoList.ToDictionary(token => token.Token.Symbol, token => token);
    }

    private async Task<List<TokenHolderInfoDto>> ConvertIndexerNftHolderInfoDtoAsync(
        List<IndexerTokenHolderInfoDto> indexerTokenHolderInfo, string chainId, string collectionSymbol)
    {
        var collectionSymbols = new List<string>() { collectionSymbol };
        
        var groupAndSumSupply = await GetCollectionSupplyAsync(chainId, collectionSymbols);       
        
        var tokenSupply = groupAndSumSupply.TryGetValue(collectionSymbol, out var sumSupply) ? sumSupply : 0;
         
        var list = new List<TokenHolderInfoDto>();

        foreach (var indexerTokenHolderInfoDto in indexerTokenHolderInfo)
        {
            var tokenHolderInfoDto =
                _objectMapper.Map<IndexerTokenHolderInfoDto, TokenHolderInfoDto>(indexerTokenHolderInfoDto);

            if (!indexerTokenHolderInfoDto.Address.IsNullOrEmpty())
            {
                tokenHolderInfoDto.Address = new CommonAddressDto
                {
                    Address = indexerTokenHolderInfoDto.Address
                };
            }
            if (tokenSupply != 0)
            {
                tokenHolderInfoDto.Percentage =
                    Math.Round((decimal)indexerTokenHolderInfoDto.Amount / tokenSupply * 100, CommonConstant.PercentageValueDecimals);
            }
            list.Add(tokenHolderInfoDto);
        }
        return list;
    }
    
    private async Task<Dictionary<string, long>> GetCollectionSupplyAsync(string chainId, List<string> collectionSymbols)
    {
        var nftInput = new TokenListInput()
        {
            ChainId = chainId,
            Types = new List<SymbolType> { SymbolType.Nft },
            CollectionSymbols = collectionSymbols,
            MaxResultCount = 1000
        };
        var nftListDto = await _tokenIndexerProvider.GetTokenListAsync(nftInput);
        var groupedResult = nftListDto.Items
            .GroupBy(token => token.CollectionSymbol)
            .Select(group => new 
            {
                CollectionSymbol = group.Key,
                SumSupply = group.Sum(token => DecimalHelper.DivideLong(token.Supply, token.Decimals))
            })
            .ToDictionary(g => g.CollectionSymbol, g => g.SumSupply);
        return groupedResult;    
    }

    private async Task<List<HolderInfo>> GetHolderInfoAsync(SymbolType symbolType, string chainId, string address)
    {
        var tokenHolderInput = new TokenHolderInput
        {
            Types = new List<SymbolType> { symbolType },
            ChainId = chainId,
            Address = address
        };
        var indexerNftHolder = await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);
       return indexerNftHolder.Items.Select(i => new HolderInfo
        {
            Balance = i.FormatAmount,
            Symbol = i.Token.Symbol
        }).ToList();
    }
}