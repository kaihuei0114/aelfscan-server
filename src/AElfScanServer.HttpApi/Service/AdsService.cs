using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AeFinder.Grains;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.Ads;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.Grains.Grain.Ads;
using AElfScanServer.Grains.State.Ads;
using AElfScanServer.HttpApi.Dtos.AdsData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.HttpApi.Service;

public interface IAdsService
{
    public Task<AdsDto> GetAds(AdsReq req);
}

public class AdsService : IAdsService, ITransientDependency
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly ILogger<AdsService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IEntityMappingRepository<AdsIndex, string> _adsRepository;
    private readonly IObjectMapper _objectMapper;

    public AdsService(IOptionsMonitor<GlobalOptions> globalOptions, ILogger<AdsService> logger,
        IClusterClient clusterClient, IEntityMappingRepository<AdsIndex, string> adsRepository,
        IObjectMapper objectMapper)
    {
        _globalOptions = globalOptions;
        _logger = logger;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _adsRepository = adsRepository;
    }

    public async Task<AdsDto> GetAds(AdsReq req)
    {
        var grainKey = GrainIdHelper.GenerateGrainId(req.Ip, req.Device, req.Label);
        var adsGrain = _clusterClient.GetGrain<IAdsGrain>(grainKey);
        var ads = await adsGrain.GetAsync();
        var curTime = DateTime.UtcNow;
        
        if (ads.AdsId.IsNullOrEmpty() || ads.VisitCount == 0 || ads.EndTime<=curTime)
        {
           
            var queryable = await _adsRepository.GetQueryableAsync();
            var adsList = queryable.Where(c => c.Label == req.Label).Where(c => c.StartTime <= curTime)
                .Where(c => c.EndTime >= curTime).OrderBy(c => c.CreateTime).Take(1).ToList();
            if (adsList.IsNullOrEmpty())
            {
                return new AdsDto();
            }

            var adsIndex = adsList.First();
            var adsDto = _objectMapper.Map<AdsIndex, AdsDto>(adsIndex);
            adsIndex.VisitCount--;
            await adsGrain.CreateAsync(adsIndex);

            return adsDto;
        }
        

        return ads;
    }
}