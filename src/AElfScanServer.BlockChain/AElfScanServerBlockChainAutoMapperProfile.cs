using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElfScanServer.BlockChain.Dtos;
using AutoMapper;

namespace AElfScanServer.BlockChain;

public class AElfScanServerBlockChainAutoMapperProfile : Profile
{
    public AElfScanServerBlockChainAutoMapperProfile()
    {
        CreateMap<IndexerTransactionDto, TransactionIndex>();
        CreateMap<TokenCreated, TokenInfoIndex>()
            .ForMember(d => d.ExternalInfo,
                opt => opt.MapFrom(s => s.ExternalInfo.Value.ToDictionary(o => o.Key, o => o.Value)))
            .ForMember(d => d.IssueChainId,
                opt => opt.MapFrom(s =>
                    s.IssueChainId == 0 ? null : ChainHelper.ConvertChainIdToBase58(s.IssueChainId)));
    }
}