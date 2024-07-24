using System;
using AElf.EntityMapping.Entities;
using AElfScanServer.Domain.Common.Entities;
using AElfScanServer.Domain.Shared.Common;
using Nest;
using Orleans;

namespace AElfScanServer.Common.Dtos.Ads;

[GenerateSerializer]
public class AdsDto
{
    [Id(0)] public string AdsId { get; set; }
    [Id(1)] public string Head { get; set; }
    [Id(2)] public string Logo { get; set; }
    [Id(3)] public string AdsText { get; set; }
    [Id(4)] public string ClickText { get; set; }
    [Id(5)] public string ClickLink { get; set; }
    [Id(6)] public string Label { get; set; }
    [Id(7)] public string CreateTime { get; set; }
    [Id(8)] public DateTime StartTime { get; set; }
    [Id(9)] public DateTime EndTime { get; set; }
    [Id(10)] public int VisitCount { get; set; }
    
}

public class AdsIndex : AElfIndexerEntity<string>, IEntityMappingEntity
{
    [Keyword]
    public override string Id
    {
        get { return AdsId; }
    }

    [Keyword] public string AdsId { get; set; }
    [Keyword] public string Head { get; set; }
    [Keyword] public string Logo { get; set; }
    [Keyword] public string AdsText { get; set; }
    [Keyword] public string ClickText { get; set; }
    [Keyword] public string ClickLink { get; set; }
    [Keyword] public string Label { get; set; }
    [Keyword] public string CreateTime { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public int VisitCount { get; set; }
}