using AElfScanServer.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftItemHolderInfoDto
{
    public CommonAddressDto Address { get; set; }
    public decimal Quantity { get; set; }
    public decimal Percentage { get; set; }
}