using System;
using System.Collections.Generic;
using AElfScanServer.Common.Dtos.Ads;

namespace AElfScanServer.HttpApi.Dtos.AdsData;

public class AdsReq
{
    public string Label { get; set; }
    public string Device { get; set; } = "";
    public string Ip { get; set; } = "";
}


public class UpdateAdsReq
{
    public string AdsId { get; set; } = "";
    public string Head { get; set; }
    public string Logo { get; set; }
    public string AdsText { get; set; }
    public string ClickText { get; set; }
    public string ClickLink { get; set; }
    public string Label { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalVisitCount { get; set; }
}

public class AdsListResp
{
    public int Total { get; set; }
    public List<AdsIndex> List { get; set; }
}

public class DeleteAdsReq
{
    public string AdsId { get; set; }
}

public class GetAdsListReq
{
    public string Label { get; set; } = "";
}

