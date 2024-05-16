using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Helper;
using NUglify.Helpers;

namespace AElfScanServer.TokenDataFunction.Dtos.Input;

public class NftInventoryInput : BaseInput
{
    public string Search { get; set; } = "";
    
    public string CollectionSymbol { get; set; }
    
    public bool IsSearchAddress()
    {
        return !Search.IsNullOrWhiteSpace() && CommomHelper.IsValidAddress(Search);
    }
}