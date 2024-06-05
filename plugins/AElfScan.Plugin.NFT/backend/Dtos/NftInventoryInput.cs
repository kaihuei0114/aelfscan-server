using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Helper;
using NUglify.Helpers;

namespace AElfScanServer.TokenDataFunction.Dtos.Input;

public class NftInventoryInput 
{
    public required string ChainId { get; set; }
    public long SkipCount { get; set; }
    public long MaxResultCount { get; set; } = 10;
    public string Search { get; set; } = "";
    
    public string CollectionSymbol { get; set; }
    
}