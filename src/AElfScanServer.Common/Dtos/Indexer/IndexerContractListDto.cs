using System.Collections.Generic;

namespace AElfScanServer.Dtos.Indexer;

public class IndexerContractListDto
{
    public List<ContractInfoDto> ContractInfo { get; set; } = new();
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