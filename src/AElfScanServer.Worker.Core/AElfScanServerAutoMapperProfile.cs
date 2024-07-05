using AElfScanServer.Common.Dtos;
using AElfScanServer.HttpApi.Dtos;
using AutoMapper;

namespace AElfScanServer.Worker.Core;

public class AelfExploreServerAutoMapperProfile : Profile
{
    public AelfExploreServerAutoMapperProfile()
    {
        CreateMap<AddressIndex, CommonAddressDto>()
            .ReverseMap();
        ;
    }
}