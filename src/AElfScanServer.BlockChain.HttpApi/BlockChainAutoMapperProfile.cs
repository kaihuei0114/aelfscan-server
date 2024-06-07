using AElfScanServer.BlockChain.Dtos;
using AutoMapper;
using AElfScanServer.Dtos;

namespace AElfScanServer.Common.BlockChainDataFunction;

public class BlockChainAutoMapperProfile : Profile
{
    public BlockChainAutoMapperProfile()
    {
        CreateMap<AddressIndex, CommonAddressDto>()
            .ReverseMap();

        CreateMap<IndexerTransactionDto, TransactionDetailDto>()
            ;
    }
}