using AElfScanServer.Common.Dtos;

namespace AElfScanServer.Common.Dtos;

public class NftInfoDto
{
    public TokenBaseInfo NftCollection { get; set; }
    public decimal Items { get; set; }
    public long Holders { get; set; }

    public long TransferCount { get; set; }
}