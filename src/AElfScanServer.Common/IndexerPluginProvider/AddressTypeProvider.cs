using System.Threading.Tasks;
using AElfScanServer.Common.Dtos;

namespace AElfScanServer.Common.IndexerPluginProvider;

public interface IAddressTypeProvider
{
    public Task<string> GetAddressType(string chainId, string address);
}