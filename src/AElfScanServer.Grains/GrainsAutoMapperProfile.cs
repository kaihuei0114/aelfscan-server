using AElfScanServer.Common.Dtos;
using AElfScanServer.Grains.State.Contract;
using AutoMapper;

namespace AElfScanServer.Grains;

public class GrainsAutoMapperProfile : Profile
{
    public GrainsAutoMapperProfile()
    {
        CreateMap<ContractFileState, ContractFileResultDto>().ReverseMap();
        CreateMap<SynchronizationState, SynchronizationDto>().ReverseMap();
    }
}