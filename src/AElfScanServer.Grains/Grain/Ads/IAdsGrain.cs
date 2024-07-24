using AElfScanServer.Common.Dtos.Ads;

namespace AElfScanServer.Grains.Grain.Ads;

public interface IAdsGrain : IGrainWithStringKey
{
    Task UpdateAsync(AdsIndex dto);

    Task CreateAsync(AdsIndex dto);
    Task<AdsDto> GetAsync();
}