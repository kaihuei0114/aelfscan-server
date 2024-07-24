using System;
using AElfScanServer.Common.Dtos.Ads;

namespace AElfScanServer.HttpApi.Dtos.AdsData;

public class AdsReq
{
    public string Label { get; set; }
    public string Device { get; set; }
    public string Ip { get; set; }
}

