using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Token.HttpApi.Dtos;

public class NftHolderInfoDto
{
    public CommonAddressDto Address { get; set; }
    public decimal Quantity { get; set; }
    public decimal Percentage { get; set; }
}