namespace AElfScanServer.Token.Dtos;

public class TokenCommonDto
{
    public TokenBaseInfo Token { get; set; }
    public decimal TotalSupply { get; set; }
    public decimal CirculatingSupply { get; set; }
    public long Holders { get; set; }
    public double HolderPercentChange24H { get; set; }
    public long TransferCount { get; set; }
}