using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetAddressTokenListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
    
    public string Search { get; set; } = "";
}

public class GetAddressTokenListResultDto
{
    public decimal AssetInUsd { get; set; }
    public decimal AssetInElf { get; set; }
    public long Total { get; set; }
    public List<TokenInfoDto> List { get; set; }
}