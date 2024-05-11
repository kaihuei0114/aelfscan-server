using System;
using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Dtos;

public class GetContractContracts : GetListInputBasicDto
{
}

public class GetContractListResultDto
{
    public long Total { get; set; }
    public List<ContractDto> List { get; set; }
}

public class ContractDto
{
    public string Address { get; set; }
    public string ContractName { get; set; } = "-";
    public string Type { get; set; }
    public int Version { get; set; }
    public decimal Balance { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public long TransactionCount { get; set; }
    public string ContractVersion { get; set; } = "-";
}