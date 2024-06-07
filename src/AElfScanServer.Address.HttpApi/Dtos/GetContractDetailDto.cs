using System.ComponentModel.DataAnnotations;

namespace AElfScanServer.Common.Address.HttpApi.Dtos;

public class GetContractDetailInput : GetDetailBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetContractDetailResultDto
{
}