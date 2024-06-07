using AElf.Contracts.MultiToken;
using AElfScanServer.BlockChain.Dtos;
using AutoMapper;
using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Worker.Core;

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