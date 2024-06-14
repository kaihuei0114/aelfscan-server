using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Common.Dtos;

namespace AElfScanServer.HttpApi.Dtos.address;

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