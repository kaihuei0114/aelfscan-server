using System.Collections.Generic;

namespace AElfScanServer.Token.Dtos;

public class TokenPriceDto
{ 
    public List<CurrencyDto> Currencies { get; set; } = new();
}

public class CurrencyDto
{
    public string BaseCurrency { get; set; }
    public string QuoteCurrency { get; set; }
}