namespace AElfScanServer.Token.Dtos;

public class TokenInfoListDto : ListResponseDto<TokenInfoDto>
{
    public double AssetInUsd { set; get; }
}

public class TokenInfoDto
{
    public TokenBaseInfo Token { set; get; }
    public decimal Quantity { set; get; }
    public decimal PriceInUsd { set; get; }
    public decimal UsdPercentChange { set; get; }
    public decimal TotalPriceInUsd { set; get; }
    public decimal PriceInElf { set; get; }
    public decimal TotalPriceInElf { set; get; }
}