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
using Microsoft.IdentityModel.Tokens;
using Orleans;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.HttpApi.Service;

public interface IAdsService
{
    public Task<AdsResp> GetAds(AdsReq req);

    public Task<AdsIndex> UpdateAds(UpdateAdsReq req);

    public Task<AdsIndex> DeleteAds(DeleteAdsReq req);


    public Task<AdsListResp> GetAdsList(GetAdsListReq req);
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


    public async Task<AdsListResp> GetAdsList(GetAdsListReq req)
    {
        var queryableAsync = await _adsRepository.GetQueryableAsync();
        if (!req.Label.IsNullOrEmpty())
        {
            queryableAsync = queryableAsync.Where(c => c.Label == req.Label);
        }

        var adsIndices = queryableAsync.Take(10000).ToList();

        var result = new AdsListResp()
        {
            List = adsIndices,
            Total = adsIndices.Count
        };
        return result;
    }

    public async Task<AdsResp> GetAds(AdsReq req)
    {
        var grainKey = GrainIdHelper.GenerateGrainId(req.Ip, req.Device, req.Label);
        var adsGrain = _clusterClient.GetGrain<IAdsGrain>(grainKey);
        var ads = await adsGrain.GetAsync();
        var curTime = DateTime.UtcNow;
        var queryable = await _adsRepository.GetQueryableAsync();
        var adsResp = new AdsResp();
        if (ads == null || ads.CurAds == null)
        {
            var adsList = queryable.Where(c => c.Label == req.Label).Where(c => c.StartTime <= curTime)
                .Where(c => c.EndTime >= curTime).OrderBy(c => c.CreateTime).Take(1).ToList();
            if (adsList.IsNullOrEmpty())
            {
                return adsResp;
            }

            var adsIndex = adsList.First();


            var adsInfoDto = _objectMapper.Map<AdsIndex, AdsInfoDto>(adsIndex);
            adsResp = _objectMapper.Map<AdsIndex, AdsResp>(adsIndex);
            adsInfoDto.VisitCount++;

            var adsDto = new AdsDto()
            {
                CurAds = adsInfoDto,
                Records = new Dictionary<string, AdsVisitFinishedRecordDto>()
            };
            await adsGrain.UpdateAsync(adsDto);


            return adsResp;
        }

        if (ads.CurAds.VisitCount == ads.CurAds.TotalVisitCount || ads.CurAds.EndTime <= curTime)
        {
            ads.Records[ads.CurAds.AdsId] = new AdsVisitFinishedRecordDto()
            {
                AdsId = ads.CurAds.AdsId,
                VisitCount = ads.CurAds.VisitCount,
                TotalVisitCount = ads.CurAds.TotalVisitCount,
                FinishedTime = curTime,
            };
            var adsList = queryable.Where(c => c.Label == req.Label).Where(c => c.StartTime <= curTime)
                .Where(c => c.EndTime >= curTime).OrderBy(c => c.CreateTime).Take(1000).ToList();
            if (adsList.IsNullOrEmpty())
            {
                return adsResp;
            }

            foreach (var adsIndex in adsList)
            {
                if (!ads.Records.ContainsKey(adsIndex.AdsId))
                {
                    var adsInfoDto = _objectMapper.Map<AdsIndex, AdsInfoDto>(adsIndex);
                    adsResp = _objectMapper.Map<AdsIndex, AdsResp>(adsIndex);
                    adsInfoDto.VisitCount++;
                    ads.CurAds = adsInfoDto;
                    await adsGrain.UpdateAsync(ads);
                    return adsResp;
                }
            }

            return adsResp;
        }


        var adsIndexList = queryable.Where(c => c.Label == req.Label).Where(c => c.AdsId == ads.CurAds.AdsId).Take(1);
        if (adsIndexList.IsNullOrEmpty())
        {
            var adsList = queryable.Where(c => c.Label == req.Label).Where(c => c.StartTime <= curTime)
                .Where(c => c.EndTime >= curTime).OrderBy(c => c.CreateTime).Take(1000).ToList();
            if (adsList.IsNullOrEmpty())
            {
                return adsResp;
            }

            foreach (var adsIndex in adsList)
            {
                if (!ads.Records.ContainsKey(adsIndex.AdsId))
                {
                    var adsInfoDto = _objectMapper.Map<AdsIndex, AdsInfoDto>(adsIndex);
                    adsResp = _objectMapper.Map<AdsIndex, AdsResp>(adsIndex);
                    adsInfoDto.VisitCount++;
                    ads.CurAds = adsInfoDto;
                    await adsGrain.UpdateAsync(ads);
                    return adsResp;
                }
            }

            return adsResp;
        }

        ads.CurAds.VisitCount++;

        await adsGrain.UpdateAsync(ads);
        return _objectMapper.Map<AdsInfoDto, AdsResp>(ads.CurAds);
    }

    public async Task<AdsIndex> UpdateAds(UpdateAdsReq req)
    {
        var adsIndex = _objectMapper.Map<UpdateAdsReq, AdsIndex>(req);
        if (req.AdsId.IsNullOrEmpty())
        {
            var adsId = Guid.NewGuid().ToString();
            adsIndex.AdsId = adsId;
            adsIndex.Id = adsId;
            adsIndex.CreateTime = DateTime.UtcNow;
            adsIndex.UpdateTime = DateTime.UtcNow;
        }

        adsIndex.UpdateTime = DateTime.UtcNow;
        await _adsRepository.AddOrUpdateAsync(adsIndex);
        return adsIndex;
    }

    public async Task<AdsIndex> DeleteAds(DeleteAdsReq req)
    {
        var queryableAsync = await _adsRepository.GetQueryableAsync();
        var adsIndices = queryableAsync.Where(c => c.AdsId == req.AdsId).Take(1);

        if (adsIndices.IsNullOrEmpty())
        {
            return new AdsIndex();
        }

        var index = new AdsIndex()
        {
            Id = req.AdsId
        };
        await _adsRepository.DeleteAsync(index);

        return adsIndices.First();
    }
}