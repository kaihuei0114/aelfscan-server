using System.Collections.Concurrent;
using AElf.Client.Service;
using AElfScanServer.Common.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Common.NodeProvider;

public interface IBlockchainClientFactory<T> 
    where T : class
{
    T GetClient(string chainName);
}

public class AElfClientFactory : IBlockchainClientFactory<AElfClient>,ISingletonDependency
{
        private readonly GlobalOptions _globalOptions;
        private readonly ConcurrentDictionary<string, AElfClient> _clientDic;

        public AElfClientFactory(IOptionsMonitor<GlobalOptions> blockChainOptions)
        {
            _globalOptions = blockChainOptions.CurrentValue;
            _clientDic = new ConcurrentDictionary<string, AElfClient>();
        }

        public AElfClient GetClient(string chainName)
        {
            var chainUrl = _globalOptions.ChainNodeHosts[chainName];
            if (_clientDic.TryGetValue(chainName, out var client))
            {
                return client;
            }

            client = new AElfClient(chainUrl);
            _clientDic[chainName] = client;
            return client;
        }
}