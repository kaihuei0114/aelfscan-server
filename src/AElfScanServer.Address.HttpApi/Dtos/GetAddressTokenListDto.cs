using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetAddressTokenListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
    [Required] public TokenType TokenType { get; set; }
    public string Sorting { get; set; }
}

public class GetAddressTokenListResultDto
{
    public double AssetInUsd { get; set; } // only used in address token list
    public long Total { get; set; }
    public List<TokenInfoDto> Tokens { get; set; }
    public List<NftInfoDto> Nfts { get; set; }
}