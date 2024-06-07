using System;
using System.Collections.Generic;
using AElfScanServer.Dtos.Indexer;

namespace AElfScanServer.Common.Token.HttpApi.Dtos.Indexer;

public class IndexerTokenHolderInfoDto
{
    public string Id { get; set; }
    public string Address { get; set; }
    public IndexerTokenBaseDto Token { get; set; }
    public long Amount { get; set; }
    public decimal FormatAmount { get; set; }
    public long TransferCount { get; set; }
    public string FirstNftTransactionId { get; set; }
    public DateTime? FirstNftTime { get; set; }
}

public class IndexerTokenHolderInfosDto
{
    public IndexerTokenHolderInfoListDto AccountToken { get; set; }
}

public class IndexerTokenHolderInfoListDto
{
    public long TotalCount  { get; set; }
    public List<IndexerTokenHolderInfoDto> Items { get; set; } = new();
}