using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AutoMapper;
using ContractRecordDto = AElfScanServer.Address.HttpApi.Dtos.ContractRecordDto;

namespace AElfScanServer.Address.HttpApi;

public class AElfScanServerAddressAutoMapperProfile : Profile
{
    public AElfScanServerAddressAutoMapperProfile()
    {
        CreateMap<GetAddressTokenListInput, GetTokenListInput>();
        CreateMap<GetAddressTokenListInput, GetNftListInput>();
        CreateMap<GetTransferListInput, TokenTransferInput>();
        CreateMap<AccountTokenDto, GetAddressInfoResultDto>().ForMember(t => t.Balance, m => m.MapFrom(f => f.Amount));
        CreateMap<ContractInfoDto, GetAddressDetailResultDto>();
        CreateMap<ContractInfoDto, ContractRecordDto>();
        CreateMap<TransactionsResponseDto, GetTransactionListResultDto>();
        CreateMap<LogEventResponseDto, GetContractEventListResultDto>();
    }
}