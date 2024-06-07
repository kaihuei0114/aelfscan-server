using System.Collections.Generic;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Token.HttpApi.Dtos.Input;

public class NftItemHolderInfoInput : BaseInput
{
    public string Symbol { get; set; } = "";
    
    public List<SymbolType> Types { get; set; } =  new() { SymbolType.Nft };
}