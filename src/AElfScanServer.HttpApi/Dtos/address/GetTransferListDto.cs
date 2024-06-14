using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.Common.Dtos;

namespace AElfScanServer.HttpApi.Dtos.address;

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