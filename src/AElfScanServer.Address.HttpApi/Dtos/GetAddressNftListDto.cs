using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Token.Dtos;
using Scriban.Parsing;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetAddressNftListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetAddressNftListResultDto
{
    public long Total { get; set; }
    public List<NftInfoDto> List { get; set; }
}