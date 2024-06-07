namespace AElfScanServer.Common.Token.Dtos.Input;

public class GetTransferInfoListInput : BaseInput
{
    public string Address { get; set; }
    public bool IsNft { get; set; }
}