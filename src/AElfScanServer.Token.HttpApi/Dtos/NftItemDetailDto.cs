using System.Collections.Generic;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftItemDetailDto
{
    public TokenBaseInfo NftCollection { get; set; }
    public TokenBaseInfo Item { get; set; }
    public long Holders { get; set; }
    public List<string> Owner { get; set; }
    public List<string> Issuer { get; set; }
    public string TokenSymbol { get; set; }
    public long Quantity { get; set; }
    public MarketInfoDto MarketPlaces { get; set; }
    public bool IsSeed { get; set; }
    public string SymbolToCreate { get; set; }
    public string Expires { get; set; }
    public ListResponseDto<PropertyDto> Properties { get; set; }
    public string Description { get; set; }
}

public class MarketInfoDto
{
    public string MarketName { get; set; }
    public string MarketLogo { get; set; }
    public string MarketUrl { get; set; }
}

public class PropertyDto
{
    public string Title { get; set; }
    public string Value { get; set; }
}