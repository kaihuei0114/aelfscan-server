using System;
using System.Collections.Generic;
using System.Transactions;
using AElf.EntityMapping.Entities;
using AElfScanServer.Dtos;
using AElfScanServer.Entities;
using Nest;

namespace AElfScanServer.BlockChain.Dtos;

public class TransactionIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return BlockHash + "_" + TransactionId; }
    }

    [Keyword] public string TransactionId { get; set; }


    [Keyword] public string From { get; set; }

    [Keyword] public string To { get; set; }

    [Keyword] public string BlockHash { get; set; }

    public long BlockHeight { get; set; }

    public DateTime BlockTime { get; set; }

    [Keyword] public string MethodName { get; set; }

    [Keyword] public string Value { get; set; }

    [Keyword] public string TxnFee { get; set; }

    public TransactionStatus Status { get; set; }
}

public class AddressIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword] public override string Id { get; set; }

    [Keyword] public string Address { get; set; }

    [Keyword] public string LowerAddress { get; set; }

    [Keyword] public AddressType AddressType { get; set; }
    [Keyword] public string Name { get; set; }

    [Keyword] public string LowerName { get; set; }
    public bool IsProducer { get; set; }

    public bool IsManager { get; set; }

    [Keyword] public string ChainId { get; set; }
}

public class TokenInfoIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword] public string TokenName { get; set; }
    [Keyword] public string LowerTokenName { get; set; }
    public long TotalSupply { get; set; }
    public long Supply { get; set; }
    public long Issued { get; set; }
    [Keyword] public string Issuer { get; set; }
    [Keyword] public string Owner { get; set; }
    public bool IsPrimaryToken { get; set; }
    public bool IsBurnable { get; set; }

    [Keyword] public string Symbol { get; set; }
    [Keyword] public string LowerSymbol { get; set; }

    [Keyword] public string CollectionSymbol { get; set; }
    [Keyword] public SymbolType SymbolType { get; set; }
    [Keyword] public string IssueChainId { get; set; }

    public long BlockHeight { get; set; }

    [Keyword] public string TransactionId { get; set; }
    public Dictionary<string, string> ExternalInfo { get; set; } = new();
}

public class BlockExtraIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return BlockHeight + "_" + BlockHash; }
    }

    public long BlockHeight { get; set; }

    public string BlockHash { get; set; }

    public long BurntFee { get; set; }
}

public class LogEventIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return ContractAddress + "_" + BlockHeight + "_" + Index; }
    }

    public long BlockHeight { get; set; }


    [Keyword] public string ContractAddress { get; set; }

    public int Index { get; set; }


    [Keyword] public string TransactionId { get; set; }

    [Keyword] public string EventName { get; set; }

    [Keyword] public string MethodName { get; set; }
}