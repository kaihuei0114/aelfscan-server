using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.OpenTelemetry.ExecutionTime;
using AElfScanServer.HttpApi.Dtos;
using AElfScanServer.HttpApi.Service;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.Ads;
using AElfScanServer.HttpApi.Dtos.AdsData;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace AElfScanServer.HttpApi.Controllers;

[RemoteService]
[ControllerName("Ads")]
[Route("api/app/ads")]
public class AdsController : AbpController
{
    private readonly IAdsService _adsService;

    public AdsController(IAdsService adsService)
    {
        _adsService = adsService;
    }
    
    [HttpGet]
    [Route("detail")]
    public async Task<AdsResp> GetAdsDetailAsync(AdsReq req)
    {
        var searchKey = HttpContext.Request.Headers["SearchKey"].FirstOrDefault();
        req.SearchKey = searchKey;
        return await _adsService.GetAds(req);
    }


    [HttpPost]
    [Route("detail")]
    public async Task<AdsIndex> UpdateAdsDetailAsync(UpdateAdsReq req)
    {
        return await _adsService.UpdateAds(req);
    }


    [HttpDelete]
    [Route("detail")]
    public async Task<AdsIndex> DeleteAdsDetailAsync(DeleteAdsReq req)
    {
      
        return await _adsService.DeleteAds(req);
    }
    
    
    [HttpPost]
    [Route("list")]
    public async Task<AdsListResp> GetAdsListAsync(GetAdsListReq req)
    {
        return await _adsService.GetAdsList(req);
    }
}