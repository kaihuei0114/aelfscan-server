using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using Microsoft.Extensions.Logging;
using Nest;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Worker.Core.Service;

public interface IAddressService
{
    Task<(long, List<AddressIndex>)> GetAddressIndexAsync(string chainId, List<string> list);
    Task BulkAddOrUpdateAsync(List<AddressIndex> list);
    Task PatchAddressInfoAsync(string chainId, string address, List<AddressIndex> list);
}

public class AddressService : IAddressService, ITransientDependency
{
    private readonly ILogger<AddressService> _logger;
    private readonly INESTRepository<AddressIndex, string> _repository;

    public AddressService(INESTRepository<AddressIndex, string> repository, ILogger<AddressService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<(long, List<AddressIndex>)> GetAddressIndexAsync(string chainId, List<string> addressList)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<AddressIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Terms(i => i.Field(f => f.Address).Terms(addressList)));
        mustQuery.Add(q => !q.Exists(e => e.Field(f => f.Name)));

        QueryContainer Filter(QueryContainerDescriptor<AddressIndex> f) => f.Bool(b => b.Must(mustQuery));

        var (total, result) = await _repository.GetListAsync(Filter, index: GenerateIndexName(chainId));
        return (total, result);
    }

    public async Task BulkAddOrUpdateAsync(List<AddressIndex> list) => await _repository.BulkAddOrUpdateAsync(list);

    public async Task PatchAddressInfoAsync(string id, string chainId, List<AddressIndex> list)
    {
        var addressIndex = await _repository.GetAsync(id, GenerateIndexName(chainId));

        if (addressIndex != null) return;

        list.Add(new AddressIndex
        {
            Id = id,
            Address = id,
            LowerAddress = id.ToLower()
        });
    }

    private string GenerateIndexName(string chainId) => BlockChainIndexNameHelper.GenerateAddressIndexName(chainId);
}