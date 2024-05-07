namespace AElfScanServer.Token.Dtos;

public class TokenCommonDto
{
    public TokenBaseInfo Token { get; set; }
    public long TotalSupply { get; set; }
    public long CirculatingSupply { get; set; }
    public long Holders { get; set; }
    public double HolderPercentChange24H { get; set; }
    public long TransferCount { get; set; }
}