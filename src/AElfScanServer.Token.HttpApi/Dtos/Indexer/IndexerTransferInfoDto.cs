using System;
using System.Collections.Generic;
using AElfScanServer.Dtos;
using AElfScanServer.Dtos.Indexer;

namespace AElfScanServer.TokenDataFunction.Dtos.Indexer;

public class IndexerTransferInfoDto
{
    public string TransactionId { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Method { get; set; }
    public long BlockHeight { get; set; }
    public DateTime BlockTime { get; set; }
    public long Amount { get; set; } 
    public decimal FormatAmount { get; set; }
    public IndexerTokenBaseDto Token { get; set; }
    public MetadataDto Metadata { get; set; }
    
    public string Status { get; set; }
    
    public List<ExternalInfoDto> ExtraProperties { get; set; } = new();
}

public class IndexerTokenTransfersDto
{
    public IndexerTokenTransferListDto TransferInfo { get; set; }
}

public class IndexerTokenTransferListDto
{
    public long TotalCount  { get; set; }
    public List<IndexerTransferInfoDto> Items { get; set; } = new();
}