using System.Collections.Generic;
using AElfScanServer.Common.Dtos;

namespace AElfScanServer.HttpApi.Dtos.address;

public class GetListInputInput : GetListInputBasicDto
{
}

public class GetAddressListResultDto
{
    public long Total { get; set; }
    public decimal TotalBalance { get; set; }
    public List<GetAddressInfoResultDto> List { get; set; }
}

public class GetAddressInfoResultDto
{
    public string Address { get; set; }
    public decimal Balance { get; set; } // auto map to balance
    public long TransactionCount { get; set; }
    public decimal Percentage { get; set; }
    public AddressType AddressType { get; set; } = AddressType.EoaAddress; //0 => Address | 1 => Contract Address
}