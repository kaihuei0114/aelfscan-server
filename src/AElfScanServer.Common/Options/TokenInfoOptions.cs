using System.Collections.Generic;
using System.Linq;
using AElfScanServer.Dtos;

namespace AElfScanServer.Options;

public class TokenInfoOptions
{
    public HashSet<string> NonResourceSymbols { get; set; } = new();

    public Dictionary<string, TokenInfo> TokenInfos { get; set; } = new();
    
    public List<MarketInfoDto> MarketInfos { get; set; }= new();
    
    public List<int> ActivityTypes { get; set; }= new();
    
    public MarketInfoDto GetMarketInfo(string marketName)
    {
        return MarketInfos.FirstOrDefault(item => item.MarketName.Equals(marketName));
    }
}

public class TokenInfo
{
    public string ImageUrl { get; set; }
}