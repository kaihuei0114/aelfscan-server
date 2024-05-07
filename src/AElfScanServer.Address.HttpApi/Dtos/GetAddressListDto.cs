using System.Collections.Generic;
using AElfScanServer.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetListInputInput : GetListInputBasicDto
{
}

public class GetAddressListResultDto
{
    public long Total { get; set; }
    public string TotalBalance { get; set; }
    public List<GetAddressInfoResultDto> List { get; set; }
}

public class GetAddressInfoResultDto
{
    public string Address { get; set; }
    public long Balance { get; set; } // auto map to balance
    public long TransactionCount { get; set; }
    public string Percentage { get; set; }
    public AddressType AddressType { get; set; } = AddressType.EoaAddress; //0 => Address | 1 => Contract Address
}