using System.Collections.Generic;
using AElfScanServer.Common.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;


public class GetAddressNftListResultDto
{
    public long Total { get; set; }
    public List<AddressNftInfoDto> List { get; set; }
}