using System.ComponentModel.DataAnnotations;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetContractDetailInput : GetDetailBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetContractDetailResultDto
{
}