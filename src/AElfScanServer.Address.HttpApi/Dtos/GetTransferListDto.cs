using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Dtos;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetTransferListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
    
    public SymbolType TokenType { get; set; } = SymbolType.Token;
}

public class GetTransferListResultDto
{
    public long Total { get; set; }
    
    public List<TokenTransferInfoDto> List { get; set; }
}