using AElf.Contracts.MultiToken;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Common.Dtos;
using AutoMapper;

namespace AElfScanServer.Worker.Core;

public class AelfExploreServerAutoMapperProfile : Profile
{
    public AelfExploreServerAutoMapperProfile()
    {
        CreateMap<AddressIndex, CommonAddressDto>()
            .ReverseMap();

        CreateMap<IndexerTransactionDto, TransactionIndex>()
            ;

        CreateMap<TokenCreated, TokenInfoIndex>();
    }
}