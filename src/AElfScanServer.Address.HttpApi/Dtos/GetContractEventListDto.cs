using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AElfScanServer.BlockChain.Dtos;

namespace AElfScanServer.Common.Address.HttpApi.Dtos;

public class GetContractEventContractsInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetContractEventListResultDto
{
    public long Total { get; set; }
    public List<LogEventIndex> LogEvents { get; set; }
}