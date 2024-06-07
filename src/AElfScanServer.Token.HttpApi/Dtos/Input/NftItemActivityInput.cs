using AElfScanServer.Token.Dtos.Input;

namespace AElfScanServer.Common.Token.HttpApi.Dtos.Input;

public class NftItemActivityInput : BaseInput
{
    public string Symbol { get; set; }
}