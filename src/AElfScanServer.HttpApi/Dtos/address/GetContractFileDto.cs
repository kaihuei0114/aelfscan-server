using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AElfScanServer.HttpApi.Dtos.address;

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