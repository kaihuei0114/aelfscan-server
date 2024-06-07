using AElfScanServer.Token.Dtos.Input;

namespace AElfScanServer.Common.Token.HttpApi.Dtos.Input;

public class NftHolderInfoInput : BaseInput
{
    public string CollectionSymbol { get; set; }
}