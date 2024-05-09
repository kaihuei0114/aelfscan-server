using System;
using System.Collections.Generic;
using System.Linq;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AutoMapper;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Helper;
using AElfScanServer.TokenDataFunction.Dtos;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
using NftInfoDto = AElfScanServer.Token.Dtos.NftInfoDto;

namespace AElfScanServer.TokenDataFunction;

public class TokenAutoMapperProfile : Profile
{
    public TokenAutoMapperProfile()
    {
        CreateMap<IndexerTokenInfoDto, TokenCommonDto>()
            .ForPath(t => t.Token.Name, m => m.MapFrom(u => u.TokenName))
            .ForPath(t => t.Token.Symbol, m => m.MapFrom(u => u.Symbol))
            .ForPath(t => t.Token.Decimals, m => m.MapFrom(u => u.Decimals))
            .ForMember(t => t.CirculatingSupply, m => m.MapFrom(u => u.Supply))
            .ForPath(t => t.Holders, m => m.MapFrom(u => u.HolderCount))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenHolderInfoDto, TokenHolderInfoDto>()
            .ForPath(t => t.Quantity, m => m.MapFrom(u => u.FormatAmount))
            .ForMember(t => t.Address, m => m.Ignore())
            .ForPath(t => t.Address.Address, m => m.MapFrom(u => u.Address))
            ;
        CreateMap<IndexerTokenInfoDto, NftInfoDto>()
            .ForPath(t => t.NftCollection.Name, m => m.MapFrom(u => u.TokenName))
            .ForPath(t => t.NftCollection.Symbol, m => m.MapFrom(u => u.Symbol))
            .ForMember(t => t.Holders, m => m.MapFrom(u => u.HolderCount))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenInfoDto, NftDetailDto>()
            .ForPath(t => t.NftCollection.Name, m => m.MapFrom(u => u.TokenName))
            .ForPath(t => t.NftCollection.Symbol, m => m.MapFrom(u => u.Symbol))
            .ForMember(t => t.Holders, m => m.MapFrom(u => u.HolderCount))
            .ReverseMap()
            ;
        CreateMap<IndexerTransferInfoDto, NftTransferInfoDto>()
            .ForPath(t => t.Item.Name, m => m.MapFrom(u => u.Token.CollectionSymbol))
            .ForPath(t => t.Item.Symbol, m => m.MapFrom(u => u.Token.Symbol))
            .ForMember(t => t.TransactionId, m => m.MapFrom(u => u.TransactionId))
            .ForMember(t => t.Value, m => m.MapFrom(u => u.FormatAmount))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenHolderInfoDto, NftInventoryDto>()
            .ForPath(t => t.Item.Name, m => m.MapFrom(u => u.Token.CollectionSymbol))
            .ForPath(t => t.Item.Decimals, m => m.MapFrom(u => u.Token.Decimals))
            .ForPath(t => t.Item.Symbol, m => m.MapFrom(u => u.Token.Symbol))
            .ReverseMap()
            ;
        CreateMap<GetTransferInfoListInput, TokenTransferInput>()
            .ForPath(t => t.Types,
                m => m.MapFrom(u =>
                    u.IsNft
                        ? new List<SymbolType>() { SymbolType.Nft_Collection, SymbolType.Nft }
                        : new List<SymbolType>() { SymbolType.Token }))
            .ReverseMap()
            ;
        CreateMap<IndexerTransferInfoDto, AddressTokenTransferInfoDto>()
            .ForMember(t => t.Amount, m => m.MapFrom(u => u.FormatAmount))
            .ForPath(t => t.Type, m => m.MapFrom(u => u.Token.Type))
            .ForMember(t => t.TransactionHash, m => m.MapFrom(u => u.TransactionId))
            .ForPath(t => t.Timestamp, m => m.MapFrom(u => OfBlockTime(u.Metadata)))
            .ForMember(t => t.From, m => m.Ignore())
            .ForMember(t => t.To, m => m.Ignore())
            .ForPath(t => t.From.Address, m => m.MapFrom(u => u.From))
            .ForPath(t => t.To.Address, m => m.MapFrom(u => u.To))
            .ReverseMap()
            ;
        CreateMap<GetTokenListInput, TokenHolderInput>()
            .ForMember(t => t.PartialSymbol, m => m.MapFrom(u => u.Keyword))
            .ForMember(t => t.Types, m => m.MapFrom(u => new List<SymbolType> { SymbolType.Token }))
            .ReverseMap()
            ;
        CreateMap<GetNftListInput, TokenHolderInput>()
            .ForMember(t => t.PartialSymbol, m => m.MapFrom(u => u.Keyword))
            .ForMember(t => t.Types, m => m.MapFrom(u => new List<SymbolType> { SymbolType.Nft }))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenHolderInfoDto, TokenInfoDto>()
            .ForMember(t => t.Quantity, m => m.MapFrom(u => u.FormatAmount))
            .ForMember(t => t.Token, m => m.Ignore())
            .ForPath(t => t.Token.Symbol, m => m.MapFrom(u => u.Token.Symbol))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenHolderInfoDto, AddressNftInfoDto>()
            .ForMember(t => t.Quantity, m => m.MapFrom(u => u.FormatAmount))
            .ForMember(t => t.Timestamp, m => m.MapFrom(u => u.FirstNftTime.Value.Millisecond))
            .ForPath(t => t.CollectionSymbol, m => m.MapFrom(u => u.Token.CollectionSymbol))
            .ReverseMap()
            ;
        CreateMap<IndexerTransferInfoDto, TokenTransferInfoDto>()
            .ForMember(t => t.ChainId, m => m.MapFrom(u => OfChainId(u.Metadata)))
            .ForMember(t => t.TransactionId, m => m.MapFrom(u => u.TransactionId))
            .ForMember(t => t.Method, m => m.MapFrom(u => u.Method))
            .ForMember(t => t.BlockHeight, m => m.MapFrom(u => OfBlockHeight(u.Metadata)))
            .ForMember(t => t.BlockTime, m => m.MapFrom(u => OfBlockTime(u.Metadata)))
            .ForMember(t => t.Quantity, m => m.MapFrom(u => u.FormatAmount))
            .ForMember(t => t.From, m => m.Ignore())
            .ForMember(t => t.To, m => m.Ignore())
            .ForPath(t => t.From.Address, m => m.MapFrom(u => u.From))
            .ForPath(t => t.To.Address, m => m.MapFrom(u => u.To))
            .ReverseMap()
            ;
        CreateMap<IndexerTokenInfoDto, NftItemDetailDto>()
            .ForPath(t => t.Item.Name, m => m.MapFrom(u => u.TokenName))
            .ForPath(t => t.Item.Symbol, m => m.MapFrom(u => u.Symbol))
            .ForPath(t => t.Item.Decimals, m => m.MapFrom(u => u.Decimals))
            .ForMember(t => t.Holders, m => m.MapFrom(u => u.HolderCount))
            .ForMember(t => t.Owner,
                m => m.MapFrom(u =>
                    string.IsNullOrEmpty(u.Owner) ? new List<string>() : new List<string> { u.Owner }))
            .ForMember(t => t.Issuer,
                m => m.MapFrom(u =>
                    string.IsNullOrEmpty(u.Owner) ? new List<string>() : new List<string> { u.Issuer }))
            .ForMember(t => t.TokenSymbol, m => m.MapFrom(u => u.Symbol))
            .ForPath(t => t.SymbolToCreate,
                m => m.MapFrom(u => OfExternalInfoKeyValue(u.ExternalInfo, "__seed_owned_symbol")))
            .ForPath(t => t.ExpireTime,
                m => m.MapFrom(u => OfExternalInfoKeyValue(u.ExternalInfo, "__seed_exp_time")))
            .ForPath(t => t.Description,
                m => m.MapFrom(u => OfExternalInfoKeyValue(u.ExternalInfo, "Description")))
            .ReverseMap()
            ;
        CreateMap<NftItemHolderInfoInput, TokenHolderInput>();
        CreateMap<TokenCommonDto, TokenDetailDto>();
    }
    
    private static string OfChainId(MetadataDto metadata)
    {
        return metadata?.ChainId;
    }
    private static long OfBlockHeight(MetadataDto metadata)
    {
        return metadata?.Block?.BlockHeight ?? 0;
    }
    
    private static long OfBlockTime(MetadataDto metadata)
    {
        var blockTime = metadata?.Block?.BlockTime;
        if (blockTime == null)
        {
            return 0;
        }
        var blockTimeNew = blockTime.Value;
        return TimeHelper.GetTimeStampFromDateTimeInSeconds(blockTimeNew);
    }
    
    private static string OfExternalInfoKeyValue(List<ExternalInfoDto> externalInfo, string key)
    {
        return externalInfo.Where(e => e.Key == key).Select(e => e.Value).FirstOrDefault();
    }
    
}