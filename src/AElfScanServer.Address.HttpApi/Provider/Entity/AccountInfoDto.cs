namespace AElfScanServer.Common.Address.HttpApi.Provider.Entity;

public class AccountInfoDto: GraphQLDto
{
    public string Address { get; set; }
    public long TokenHoldingCount { get; set; }
    public long TransferCount { get; set; }
}