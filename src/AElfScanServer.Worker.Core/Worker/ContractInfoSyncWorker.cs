using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElfScanServer.Worker.Core.Dtos;
using AElfScanServer.Worker.Core.Options;
using AElfScanServer.Worker.Core.Service;
using AElfScanServer.HttpClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AElfScanServer.Common.Worker.Core.Worker;

public class ContractInfoSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IHttpProvider _httpProvider;
    private readonly WorkerOptions _workerOptions;
    private readonly IAddressService _addressService;
    private readonly ContractInfoSyncWorkerOptions _options;
    private readonly ILogger<ContractInfoSyncWorker> _logger;

    public ContractInfoSyncWorker(ILogger<ContractInfoSyncWorker> logger, IHttpProvider httpProvider,
        IOptionsSnapshot<WorkerOptions> workerOptions, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<ContractInfoSyncWorkerOptions> options, AbpAsyncTimer timer,
        IAddressService addressService) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpProvider = httpProvider;
        _addressService = addressService;
        _workerOptions = workerOptions.Value;
        Timer.Period = 1000 * _options.ExecuteInterval;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // var tasks = _workerOptions.Chains.Select(ExecuteSearchAsync);
        // await Task.WhenAll(tasks);
    }

    private async Task ExecuteSearchAsync(ChainOptionDto option)
    {
        var getContractInfoListResult = await _httpProvider.InvokeAsync<GetContractsInfoResponseDto>(
            option.BasicInfoUrl, new ApiInfo(HttpMethod.Get, _options.ContractInfosUri),
            param: new Dictionary<string, string>
            {
                { "pageSize", "500" },
                { "pageNum", "1" }
            });
        var contractInfoList = getContractInfoListResult.Data.List;

        var (total, addressIndexList) = await _addressService.GetAddressIndexAsync(option.ChainId,
            contractInfoList.Where(p => p.ContractName != "-1").Select(a => a.Address).ToList());
        addressIndexList.ForEach(p =>
            p.Name = contractInfoList.FirstOrDefault(s => s.Address == p.Address)?.ContractName);

        if (total == 0)
        {
            _logger.LogDebug("No need update contract information");
            return;
        }

        await _addressService.BulkAddOrUpdateAsync(addressIndexList);
    }
}