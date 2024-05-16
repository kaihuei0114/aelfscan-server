using AElfScanServer.BlockChain.Dtos;
using AutoMapper;
using AElfScanServer.Dtos;

namespace AElfScanServer.BlockChainDataFunction;

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