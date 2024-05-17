using System.Collections.Generic;

namespace AElfScanServer.Options;

public class NetWorkReflectionOptions
{
    public Dictionary<string, string> ReflectionItems { get; set; } = new();

    public Dictionary<string, string> SymbolItems { get; set; } = new();
}