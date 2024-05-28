using System.Collections.Generic;

namespace AElfScanServer.Dtos.Indexer;

public class IndexerContractListResultDto
{
    public IndexerContractListDto ContractList { get; set; }
}

public class IndexerContractListDto
{
    public long TotalCount { get; set; }
    public List<ContractInfoDto> Items { get; set; } = new();
}

public class ContractInfoDto
{
    public string Address { get; set; }
    public string CodeHash { get; set; }
    public string Author { get; set; }
    public int Version { get; set; }
    public string NameHash { get; set; }
    public string ContractVersion { get; set; }
    public int ContractCategory { get; set; }
    public string ContractType { get; set; }
    public MetadataDto Metadata { get; set; } = new MetadataDto();
}