using AElfScanServer.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftItemActivityDto
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public string Action { get; set; }
    public long BlockTime { get; set; }
    public long BlockHeight { get; set; }
    public decimal PriceOfUsd { get; set; }
    public decimal Price { get; set; }
    public string PriceSymbol { get; set; }
    public decimal Quantity { get; set; }
    public CommonAddressDto From { get; set; }
    public CommonAddressDto To { get; set; }
}