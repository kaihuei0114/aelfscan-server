namespace AElfScanServer.Token.Dtos.Input;

public class BaseInput
{
    public required string ChainId { get; set; }
    public long SkipCount { get; set; }
    public long MaxResultCount { get; set; } = 10;
    
    public string OrderBy { get; set; }
    
    public string Sort { get; set; }
}
