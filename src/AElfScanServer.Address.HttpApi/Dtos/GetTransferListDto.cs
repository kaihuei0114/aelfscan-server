using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetTransferListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
    //public TokenType TokenType { get; set; } = TokenType.Token;
}

public class GetTransferListResultDto
{
    public double AssetInUsd { get; set; } // only used in token transfer list
    public long Total { get; set; }
    public List<TokenTransferInfoDto> Tokens { get; set; }
    public List<TokenTransferInfoDto> Nfts { get; set; }
}