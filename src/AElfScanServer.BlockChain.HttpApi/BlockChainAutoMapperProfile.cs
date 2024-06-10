using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Common.Dtos;
using AutoMapper;

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