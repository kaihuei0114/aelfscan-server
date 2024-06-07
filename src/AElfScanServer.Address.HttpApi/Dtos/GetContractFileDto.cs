using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AElfScanServer.Common.Address.HttpApi.Dtos;

public class GetContractFileInput : GetDetailBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetContractFileResultDto
{
    public string ContractName { get; set; }
    public string ContractVersion { get; set; }
    public List<DecompilerContractDto> ContractSourceCode { get; set; }
}