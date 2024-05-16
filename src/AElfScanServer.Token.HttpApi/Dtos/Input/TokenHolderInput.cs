using System;
using System.Collections.Generic;
using AElfScanServer.Token.Dtos.Input;
using AElfScanServer.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos.Input;

public class TokenHolderInput : BaseInput
{
    public string Symbol { get; set; } = "";
    public string CollectionSymbol { get; set; } = "";
    public string Address { get; set; } = "";
    public string PartialSymbol { get; set; } = "";
    
    public string Search { get; set; } = "";

    public List<SymbolType> Types { get; set; } =  new() { SymbolType.Token };
    
    public List<string> Symbols { get; set; } = new();
    
    //symbol or collection symbol
    public List<string> SearchSymbols { get; set; } = new();
    
    public void SetDefaultSort()
    {
        if (!Sort.IsNullOrEmpty())
        {
            return;
        }

        Sort = "Desc";
        OrderBy = "FormatAmount";
    }
}