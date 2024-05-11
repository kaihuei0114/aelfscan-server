using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Token.Dtos;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.TokenDataFunction.Dtos.Indexer;
using AElfScanServer.TokenDataFunction.Dtos.Input;
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
            
        CreateMap<IndexerTokenHolderInfoDto, GetAddressInfoResultDto>()
            .ForPath(t => t.Balance, m => m.MapFrom(u => u.FormatAmount))
            .ForPath(t => t.TransactionCount, m => m.MapFrom(u => u.TransferCount))
            ;
        CreateMap<GetAddressTokenListInput, TokenHolderInput>();
        CreateMap<IndexerTokenHolderInfoDto, TokenInfoDto>()
            .ForPath(t => t.Token.Name, m => m.MapFrom(u => u.Token.CollectionSymbol))
            .ForPath(t => t.Token.Decimals, m => m.MapFrom(u => u.Token.Decimals))
            .ForPath(t => t.Token.Symbol, m => m.MapFrom(u => u.Token.Symbol))
            .ForPath(t => t.Quantity, m => m.MapFrom(u => u.FormatAmount))
            ;
    }
}