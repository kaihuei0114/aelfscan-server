using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Common.Dtos;
using AutoMapper;

namespace AElfScanServer.BlockChain.HttpApi;

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