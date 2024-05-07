namespace AElfScanServer.Token.Dtos;

public class NftInfoListDto : ListResponseDto<AddressNftInfoDto>
{
    
}

public class AddressNftInfoDto
{
    public TokenBaseInfo Item { set; get; }
    public string Collection { set; get; }
    public string CollectionSymbol { set; get; }
    public decimal Quantity { set; get; }
    public long Timestamp { set; get; }
}