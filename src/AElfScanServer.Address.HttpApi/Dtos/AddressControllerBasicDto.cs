using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetDetailBasicDto
{
    [Required] public string ChainId { get; set; }
}

public class GetListInputBasicDto : PagedResultRequestDto
{
    [Required] public string ChainId { get; set; }
}

public enum TokenType
{
    Token,
    Nft
}