namespace AElfScanServer.Grains.State.Ads;

[GenerateSerializer]
public class AdsState
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