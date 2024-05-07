using AElfScanServer.Dtos;

namespace AElfScanServer.Token.Dtos;

public class TokenHolderInfoDto
{
    public CommonAddressDto Address { get; set; }
    public decimal Quantity { get; set; }
    public decimal Percentage { get; set; }
    public decimal Value { get; set; }
}