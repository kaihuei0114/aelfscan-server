using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Address.HttpApi.Options;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Constant;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.Options;
using AElfScanServer.Token;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Provider;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using AElfScanServer.TokenDataFunction.Provider;
using AElfScanServer.TokenDataFunction.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.ObjectMapping;
using TokenPriceDto = AElfScanServer.Dtos.TokenPriceDto;

namespace AElfScanServer.Address.HttpApi.AppServices;

public interface IAddressAppService
{
    Task<GetAddressListResultDto> GetAddressListAsync(GetListInputInput input);
    Task<GetAddressDetailResultDto> GetAddressDetailAsync(GetAddressDetailInput input);
    Task<GetAddressTokenListResultDto> GetAddressTokenListAsync(GetAddressTokenListInput input);
    Task<GetAddressNftListResultDto> GetAddressNftListAsync(GetAddressTokenListInput input);
    Task<GetTransferListResultDto> GetTransferListAsync(GetTransferListInput input);
    Task<GetTransactionListResultDto> GetTransactionListAsync(GetTransactionListInput input);
}

public class AddressAppService : IAddressAppService
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<AddressAppService> _logger;
    private readonly IBlockChainProvider _blockChainProvider;
    private readonly IIndexerGenesisProvider _indexerGenesisProvider;
    private readonly ITokenIndexerProvider _tokenIndexerProvider;
    private readonly ITokenPriceService _tokenPriceService;
    private readonly ITokenInfoProvider _tokenInfoProvider;
    private readonly IOptionsMonitor<TokenInfoOptions> _tokenInfoOptions;
    private readonly BlockChainOptions _blockChainOptions;

    public AddressAppService(IObjectMapper objectMapper, ILogger<AddressAppService> logger,
        BlockChainProvider blockChainProvider, IIndexerGenesisProvider indexerGenesisProvider,
        ITokenIndexerProvider tokenIndexerProvider, ITokenPriceService tokenPriceService,
        ITokenInfoProvider tokenInfoProvider, IOptionsMonitor<TokenInfoOptions> tokenInfoOptions,
        IOptionsSnapshot<BlockChainOptions> blockChainOptions)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _blockChainProvider = blockChainProvider;
        _indexerGenesisProvider = indexerGenesisProvider;
        _tokenIndexerProvider = tokenIndexerProvider;
        _tokenPriceService = tokenPriceService;
        _tokenInfoProvider = tokenInfoProvider;
        _tokenInfoOptions = tokenInfoOptions;
        _blockChainOptions = blockChainOptions.Value;
    }

    public async Task<GetAddressListResultDto> GetAddressListAsync(GetListInputInput input)
    {
        var holderInput = new TokenHolderInput
        {
            ChainId = input.ChainId, Symbol = CurrencyConstant.ElfCurrency,
            SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount
        };
        holderInput.SetDefaultSort();
        var indexerTokenHolderInfo = await _tokenIndexerProvider.GetTokenHolderInfoAsync(holderInput);
        var indexerTokenList =
            await _tokenIndexerProvider.GetTokenDetailAsync(input.ChainId, CurrencyConstant.ElfCurrency);
        var tokenInfo = indexerTokenList[0];
        var result = new GetAddressListResultDto
        {
            Total = indexerTokenHolderInfo.TotalCount,
            TotalBalance = DecimalHelper.Divide(tokenInfo.Supply, tokenInfo.Decimals)
        };
        //TODO to get address infos
        var addressInfos = await _blockChainProvider.GetAddressDictionaryAsync(new AElfAddressInput
        {
            ChainId = input.ChainId,
            Addresses = indexerTokenHolderInfo.Items.Select(address => address.Address).ToList()
        });
        var addressList = new List<GetAddressInfoResultDto>();
        foreach (var info in indexerTokenHolderInfo.Items)
        {
            var addressResult = _objectMapper.Map<IndexerTokenHolderInfoDto, GetAddressInfoResultDto>(info);
            addressResult.Percentage = Math.Round((decimal)info.Amount / tokenInfo.Supply * 100,
                CommonConstant.PercentageValueDecimals);
            if (addressInfos.TryGetValue(info.Address, out var addressInfo))
            {
                addressResult.AddressType = addressInfo.AddressType;
            }

            addressList.Add(addressResult);
        }

        //add sort 
        addressList = addressList.OrderByDescending(item => item.Balance)
            .ThenByDescending(item => item.TransactionCount)
            .ToList();
        result.List = addressList;
        return result;
    }

    public async Task<GetAddressDetailResultDto> GetAddressDetailAsync(GetAddressDetailInput input)
    {
        _logger.LogInformation("GetAddressDetailAsync");
        var result = new GetAddressDetailResultDto();

        if (_blockChainOptions.ContractNames.TryGetValue(input.ChainId, out var contractNames))
        {
            if (contractNames.TryGetValue(input.Address, out var name))
            {
                var contractInfo = await _indexerGenesisProvider.GetContractAsync(input.ChainId, input.Address);
                result = _objectMapper.Map<ContractInfoDto, GetAddressDetailResultDto>(contractInfo);
                result.Author = contractInfo.Author;
                result.ContractName = name;
            }
        }

        // if (input.AddressType is AddressType.ContractAddress)
        // {
        //     var contractInfo = await _indexerGenesisProvider.GetContractAsync(input.ChainId, input.Address);
        //     result = _objectMapper.Map<ContractInfoDto, GetAddressDetailResultDto>(contractInfo);
        //     var addressInfos = await _blockChainProvider.GetAddressDictionaryAsync(new AElfAddressInput
        //     {
        //         Addresses = new List<string>(new[] { input.Address })
        //     });
        //
        //     result.ContractName = addressInfos.TryGetValue(input.Address, out var addressInfo)
        //         ? addressInfo.Name
        //         : "ContractName";
        //     // todo: indexer add time sort
        //     /*var contractRecords = await _indexerGenesisProvider.GetContractRecordAsync(input.ChainId, input.Address);
        //     if (contractRecords.Count > 0)
        //     {
        //         result.ContractTransactionHash = contractRecords[0].TransactionId;
        //     }*/
        // }

        var holderInfo =
            await _tokenIndexerProvider.GetHolderInfoAsync(input.ChainId, CurrencyConstant.ElfCurrency, input.Address);
        var priceDto =
            await _tokenPriceService.GetTokenPriceAsync(CurrencyConstant.ElfCurrency, CurrencyConstant.UsdCurrency);
        result.ElfBalance = holderInfo.Balance;
        result.ElfPriceInUsd = Math.Round(priceDto.Price, CommonConstant.UsdValueDecimals);
        result.ElfBalanceOfUsd = Math.Round(holderInfo.Balance * priceDto.Price, CommonConstant.UsdValueDecimals);
      
        var types = new List<SymbolType> { SymbolType.Token, SymbolType.Nft };
        var holderInfos = await _tokenIndexerProvider.GetHolderInfoAsync(input.ChainId, input.Address, types);
        result.TokenHoldings = holderInfos.Count;

        var transferInput = new TokenTransferInput()
        {
            ChainId = input.ChainId
        };
        transferInput.SetDefaultSort();
        var tokenTransferListDto = await _tokenIndexerProvider.GetTokenTransferInfoAsync(transferInput);

        if (!tokenTransferListDto.Items.IsNullOrEmpty())
        {
            var transferInfoDto = tokenTransferListDto.Items[0];
            result.LastTransactionSend = new TransactionInfoDto
            {
                TransactionId = transferInfoDto.TransactionId,
                BlockHeight = transferInfoDto.Metadata.Block.BlockHeight,
                BlockTime = transferInfoDto.Metadata.Block.BlockTime
            };
            //TODO
            result.FirstTransactionSend = new TransactionInfoDto
            {
                TransactionId = transferInfoDto.TransactionId,
                BlockHeight = transferInfoDto.Metadata.Block.BlockHeight,
                BlockTime = transferInfoDto.Metadata.Block.BlockTime
            };
        }

        return result;
    }
    
    public async Task<GetAddressTokenListResultDto> GetAddressTokenListAsync(
        GetAddressTokenListInput input)
    {
        Dictionary<string, IndexerTokenInfoDto> tokenDict;
        IndexerTokenHolderInfoListDto holderInfos;
        //search token name or symbol
        if (!input.Search.IsNullOrWhiteSpace())
        {
            var tokenListInput = _objectMapper.Map<GetAddressTokenListInput, TokenListInput>(input);
            var tokenInfos = await _tokenIndexerProvider.GetAllTokenInfosAsync(tokenListInput);
            if (tokenInfos.IsNullOrEmpty())
            {
                return new GetAddressTokenListResultDto();
            }
            tokenDict = tokenInfos.ToDictionary(i => i.Symbol, i => i);
            holderInfos = await GetTokenHolderInfosAsync(input, searchSymbols: tokenDict.Keys.ToList());
            if (holderInfos.Items.IsNullOrEmpty())
            {
                return new GetAddressTokenListResultDto();
            }
        }
        else
        {
            holderInfos = await GetTokenHolderInfosAsync(input);
            if (holderInfos.Items.IsNullOrEmpty())
            {
                return new GetAddressTokenListResultDto();
            }
            tokenDict = await _tokenIndexerProvider.GetTokenDictAsync(input.ChainId,
                holderInfos.Items.Select(i => i.Token.Symbol).ToList());
        }
        
        var elfPriceDto =
            await _tokenPriceService.GetTokenPriceAsync(CurrencyConstant.ElfCurrency, CurrencyConstant.UsdCurrency);
        var tokenInfoList = await GetTokenInfoListAsync(holderInfos.Items, tokenDict, elfPriceDto);

        return new GetAddressTokenListResultDto
        {
            AssetInUsd = tokenInfoList.Sum(i => i.ValueOfUsd),
            Total = holderInfos.TotalCount,
            List = tokenInfoList
        };
    }

    public async Task<GetAddressNftListResultDto> GetAddressNftListAsync(GetAddressTokenListInput input)
    {
        IndexerTokenHolderInfoListDto holderInfos;
        var types = new List<SymbolType> { SymbolType.Nft };
        if (!input.Search.IsNullOrWhiteSpace())
        {
            var tokenListInputNft = _objectMapper.Map<GetAddressTokenListInput, TokenListInput>(input);
            tokenListInputNft.Types = types;
        
            var tokenListInputCollection = _objectMapper.Map<GetAddressTokenListInput, TokenListInput>(input);
            tokenListInputCollection.Types = new List<SymbolType> { SymbolType.Nft_Collection };

            var nftInfosTask = _tokenIndexerProvider.GetAllTokenInfosAsync(tokenListInputNft);
            var collectionInfosTask = _tokenIndexerProvider.GetAllTokenInfosAsync(tokenListInputCollection);
            await Task.WhenAll(nftInfosTask, collectionInfosTask);

            var nftInfos = nftInfosTask.Result;
            var collectionInfos = collectionInfosTask.Result;
            
            if (nftInfos.IsNullOrEmpty() && collectionInfos.IsNullOrEmpty())
            {
                return new GetAddressNftListResultDto();
            }
            var searchSymbols = new List<string>(nftInfos.Select(i => i.Symbol).ToHashSet());;
            var searchCollectionSymbols = new List<string>(collectionInfos.Select(i => i.Symbol).ToHashSet());
            searchSymbols.AddRange(searchCollectionSymbols);
            holderInfos = await GetTokenHolderInfosAsync(input, types, searchSymbols: searchSymbols);
            if (holderInfos.Items.IsNullOrEmpty())
            {
                return new GetAddressNftListResultDto();
            }
        }
        else
        {
            holderInfos = await GetTokenHolderInfosAsync(input, types);
            if (holderInfos.Items.IsNullOrEmpty())
            {
                return new GetAddressNftListResultDto();
            }
        }
        var collectionSymbols = new List<string>(holderInfos.Items.Select(i => i.Token.CollectionSymbol).ToHashSet());
        var symbols = new List<string>(holderInfos.Items.Select(i => i.Token.Symbol).ToHashSet());
        symbols.AddRange(collectionSymbols);
        var tokenDict = await _tokenIndexerProvider.GetTokenDictAsync(input.ChainId, symbols);
        var list = await CreateNftInfoListAsync(holderInfos.Items, tokenDict);
        var result = new GetAddressNftListResultDto
        {
            Total = holderInfos.TotalCount,
            List = list
        };
        return result;
    }

    public async Task<GetTransferListResultDto> GetTransferListAsync(GetTransferListInput input)
    {
        var tokenTransferInput = _objectMapper.Map<GetTransferListInput, TokenTransferInput>(input);
        tokenTransferInput.Types = new List<SymbolType> { input.TokenType };
        var tokenTransferInfos = await _tokenIndexerProvider.GetTokenTransfersAsync(tokenTransferInput);
        return new GetTransferListResultDto
        {
            Total = tokenTransferInfos.Total,
            List = tokenTransferInfos.List
        };
    }

    public async Task<GetTransactionListResultDto> GetTransactionListAsync(GetTransactionListInput input)
        => _objectMapper.Map<TransactionsResponseDto, GetTransactionListResultDto>(
            await _blockChainProvider.GetTransactionsAsync(input.ChainId, input.Address));
    
    private async Task<Dictionary<string, IndexerTokenInfoDto>> GetTokenDictionaryAsync(GetAddressTokenListInput input)
    {
        var tokenListInput = _objectMapper.Map<GetAddressTokenListInput, TokenListInput>(input);
        var tokenInfos = await _tokenIndexerProvider.GetTokenListAsync(tokenListInput);
        return tokenInfos.Items.ToDictionary(i => i.Symbol, i => i);
    }
    
    private async Task<IndexerTokenHolderInfoListDto> GetTokenHolderInfosAsync(GetAddressTokenListInput input, List<SymbolType> types = null, 
        List<string> searchSymbols = null)
    {
        var tokenHolderInput = _objectMapper.Map<GetAddressTokenListInput, TokenHolderInput>(input);
        tokenHolderInput.SetDefaultSort();
        if (types != null)
        {
            tokenHolderInput.Types = types;
        }

        if (searchSymbols != null)
        {
            tokenHolderInput.SearchSymbols = searchSymbols;
        }

        return await _tokenIndexerProvider.GetTokenHolderInfoAsync(tokenHolderInput);
    }

    private async Task<List<TokenInfoDto>> GetTokenInfoListAsync(IEnumerable<IndexerTokenHolderInfoDto> holderInfos,
        Dictionary<string, IndexerTokenInfoDto> tokenDict, TokenPriceDto elfPriceDto)
    {
        var list = new List<TokenInfoDto>();

        var tasks = holderInfos.Select(async holderInfo =>
        {
            var tokenHolderInfo = _objectMapper.Map<IndexerTokenHolderInfoDto, TokenInfoDto>(holderInfo);
            var symbol = holderInfo.Token.Symbol;

            if (tokenDict.TryGetValue(symbol, out var tokenInfo))
            {
                // handle image url
                tokenHolderInfo.Token.Name = tokenInfo.TokenName;
                tokenHolderInfo.Token.ImageUrl = TokenInfoHelper.GetImageUrl(tokenInfo.ExternalInfo,
                    () => _tokenInfoProvider.BuildImageUrl(tokenInfo.Symbol));
            }

            if (_tokenInfoOptions.CurrentValue.NonResourceSymbols.Contains(symbol))
            {
                var priceDto = await _tokenPriceService.GetTokenPriceAsync(symbol, CurrencyConstant.UsdCurrency);
                var timestamp = TimeHelper.GetTimeStampFromDateTime(DateTime.Today);
                var priceHisDto =
                    await _tokenPriceService.GetTokenHistoryPriceAsync(symbol, CurrencyConstant.UsdCurrency, timestamp);

                tokenHolderInfo.PriceOfUsd = Math.Round(priceDto.Price, CommonConstant.UsdValueDecimals);
                tokenHolderInfo.ValueOfUsd = Math.Round(tokenHolderInfo.Quantity * priceDto.Price,
                    CommonConstant.UsdValueDecimals);
                tokenHolderInfo.PriceOfElf =
                    Math.Round(priceDto.Price / elfPriceDto.Price, CommonConstant.ElfValueDecimals);
                tokenHolderInfo.ValueOfElf = Math.Round(tokenHolderInfo.Quantity * priceDto.Price / elfPriceDto.Price,
                    CommonConstant.ElfValueDecimals);

                if (priceHisDto.Price > 0)
                {
                    tokenHolderInfo.PriceOfUsdPercentChange24h = (double)Math.Round(
                        (priceDto.Price - priceHisDto.Price) / priceHisDto.Price * 100,
                        CommonConstant.PercentageValueDecimals);
                }
            }
            return tokenHolderInfo;
        }).ToList();

        list.AddRange(await Task.WhenAll(tasks));
        return list;
    }

    private async Task<List<AddressNftInfoDto>> CreateNftInfoListAsync(
        List<IndexerTokenHolderInfoDto> holderInfos, Dictionary<string, IndexerTokenInfoDto> tokenDict)
    {
        var tasks = holderInfos.Select(async holderInfo =>
        {
            var tokenHolderInfo = _objectMapper.Map<IndexerTokenHolderInfoDto, AddressNftInfoDto>(holderInfo);
            var symbol = holderInfo.Token.Symbol;
            var collectionSymbol = holderInfo.Token.CollectionSymbol;

            if (tokenDict.TryGetValue(symbol, out var tokenInfo))
            {
                tokenHolderInfo.Token = _tokenInfoProvider.OfTokenBaseInfo(tokenInfo);
            }

            if (tokenDict.TryGetValue(collectionSymbol, out var collectionInfo))
            {
                tokenHolderInfo.NftCollection = _tokenInfoProvider.OfTokenBaseInfo(collectionInfo);
            }

            return tokenHolderInfo;
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}