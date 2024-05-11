using System;
using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Provider.Entity;

public class IndexerContractListDto
{
    public List<ContractInfoDto> ContractInfo { get; set; }
}

public class ContractInfoDto
{
    // public string Id { get; set; }
    // public string ChainId { get; set; }
    // public string BlockHash { get; set; }
    // public long BlockHeight { get; set; }
    public string Address { get; set; }
    public string CodeHash { get; set; }
    public string Author { get; set; }
    public int Version { get; set; }
    public string NameHash { get; set; }
    public string ContractVersion { get; set; }
    public int ContractCategory { get; set; }
    public ContractType ContractType { get; set; }
    public MetadataDto Metadata { get; set; } = new MetadataDto();
}

public class MetadataDto
{
    public string ChainId { get; set; }

    public BlockMetadataDto Block { get; set; }
}

public class BlockMetadataDto
{
    public string BlockHash { get; set; }

    public long BlockHeight { get; set; }

    public DateTime BlockTime { get; set; }
}

public enum ContractType
{
    SystemContract,
    UserContract
}