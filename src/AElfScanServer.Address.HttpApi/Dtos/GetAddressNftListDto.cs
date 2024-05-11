using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetAddressNftListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetAddressNftListResultDto
{
    public long Total { get; set; }
    public List<AddressNftInfoDto> List { get; set; }
}