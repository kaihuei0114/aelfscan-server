namespace AElfScanServer.Token.Dtos;

public class NftInfoDto
{
    public TokenBaseInfo NftCollection { get; set; }
    public long Items { get; set; }
    public long Holders { get; set; }

    public long TransferCount { get; set; }
}