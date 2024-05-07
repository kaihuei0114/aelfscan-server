using System.Collections.Generic;

namespace AElfScanServer.Options;

public class ApiClientOption
{
    public List<ApiServer> ApiServers { get; set; }
}

public class ApiServer
{
    public string ServerName { get; set; }
    public string Domain { get; set; }
}

public class TokenServerOption
{
    public string Url { get; set; }
}

public class BlockChainOption
{
    public string Url { get; set; }
}