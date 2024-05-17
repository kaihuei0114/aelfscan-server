using System.Collections.Generic;

namespace AElfScanServer.Token.Dtos;

public class GetNftCollectionListResponseDto
{
    public long Total { get; set; }
    public List<NftInfoDto> List { get; set; }
}