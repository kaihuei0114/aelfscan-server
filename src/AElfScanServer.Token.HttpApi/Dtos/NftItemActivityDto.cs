using AElfScanServer.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftItemActivityDto
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public string Action { get; set; }
    public long BlockTime { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public CommonAddressDto From { get; set; }
    public CommonAddressDto To { get; set; }
    public MarketInfoDto MarketPlaces { get; set; }
}