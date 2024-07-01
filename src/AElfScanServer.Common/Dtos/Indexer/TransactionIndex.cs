using System;
using System.Collections.Generic;
using AElf.EntityMapping.Entities;
using AElfScanServer.Common.Enums;
using AElfScanServer.Domain.Common.Entities;
using Nest;

namespace AElfScanServer.Common.Dtos.Indexer;

public class TransactionIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword] public string TransactionId { get; set; }

    [Keyword] public string ChainId { get; set; }

    [Keyword] public string From { get; set; }

    [Keyword] public string To { get; set; }

    // public string BlockHash { get; set; }

    [Keyword] public string Signature { get; set; }
    public long BlockHeight { get; set; }

    // public string PreviousBlockHash { get; set; }

    [Keyword] public DateTime BlockTime { get; set; }

    [Keyword] public string MethodName { get; set; }


    [Keyword] public string DateStr { get; set; }
    public TransactionStatus Status { get; set; }

    public bool Confirmed { get; set; }

    public Dictionary<string, string> ExtraProperties { get; set; }

    public List<IndexerLogEventDto> LogEvents { get; set; }
}

public class IndexerLogEventDto
{
    // public string ChainId { get; set; }
    // public string BlockHash { get; set; }


    // public long BlockHeight { get; set; }

    // public string PreviousBlockHash { get; set; }

    // public string TransactionId { get; set; }

    // public DateTime BlockTime { get; set; }

    [Keyword] public string ContractAddress { get; set; }

    [Keyword] public string EventName { get; set; }

    public int Index { get; set; }

    // public bool Confirmed { get; set; }

    public Dictionary<string, string> ExtraProperties { get; set; }
}