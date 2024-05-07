using System;
using System.Collections.Generic;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos;
using AElfScanServer.Helper;

namespace AElfScanServer.Token.Dtos;

public class TokenTransferInput : BaseInput
{
    public string Symbol { get; set; } = "";
    public string Search { get; set; } = "";
    public string CollectionSymbol { get; set; } = "";

    public string Address { get; set; } = "";

    public List<SymbolType> Types { get; set; } = new() { SymbolType.Token };
    
    public bool IsSearchAddress()
    {
        return !Search.IsNullOrWhiteSpace() && CommomHelper.IsValidAddress(Search);
    }

}