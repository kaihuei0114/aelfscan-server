using System.Collections.Generic;

namespace AElfScanServer.BFF.Core.Adaptor;

public class HttpDirective
{
    public string Url { get; set; }
    public string Method { get; set; }
    public string Path { get; set; }
    public string RequestMediaType { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public Dictionary<string, string> Params { get; set; }
}