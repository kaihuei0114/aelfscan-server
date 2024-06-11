using System.Collections.Generic;

namespace AElfScanServer.Token.HttpApi.Options;

public class WorkerOptions
{
    public List<ChainInfo> ChainInfos { get; set; }
    public int BatchSize { get; set; } = 1000;
    
    public Dictionary<string, Worker> Workers { get; set; } = new Dictionary<string, Worker>();
    
    public int GetWorkerPeriodMinutes(string workerName)
    {
        var minutes = Workers.TryGetValue(workerName, out var worker) ? worker.Minutes : Worker.DefaultMinutes;
        return minutes;
    }
}
public class ChainInfo
{
    public string ChainId { get; set; }
}

public class Worker
{
    public const int DefaultMinutes = 10;

    public int Minutes { get; set; } = DefaultMinutes;

    public bool OpenSwitch { get; set; } = true;
}