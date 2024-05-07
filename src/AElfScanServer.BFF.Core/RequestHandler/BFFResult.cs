using Newtonsoft.Json;

namespace AElfScanServer.BFF.Core.RequestHandler;

public class BffResult
{
    [JsonProperty("code")] public long Code { get; set; }
    [JsonProperty("message")] public string Message { get; set; }
    [JsonProperty("data")] public object Data { get; set; }

    public static BffResult Success(object data = null)
    {
        return new BffResult
        {
            Code = 200,
            Message = "success",
            Data = data
        };
    }

    public static BffResult Failure(string message)
    {
        return new BffResult
        {
            Code = 500,
            Message = message,
            Data = null
        };
    }
}