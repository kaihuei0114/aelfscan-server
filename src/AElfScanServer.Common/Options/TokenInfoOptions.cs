using System.Collections.Generic;

namespace AElfScanServer.Options;

public class TokenInfoOptions
{
    public HashSet<string> NonResourceSymbols { get; set; } = new();

    public Dictionary<string, TokenInfo> TokenInfos { get; set; }
}

public class TokenInfo
{
    public string ImageUrl { get; set; }
}