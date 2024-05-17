using System.Collections.Generic;

namespace AElfScanServer.Token.Dtos.Input;

public class GetNftCollectionInfoInput
{
    public required string ChainId { get; set; }
    
    public List<string> CollectionSymbolList  { get; set; }
}