using AElfScanServer.Common.Dtos.Ads;
using AElfScanServer.Grains.State.Ads;
using Orleans.Core;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.Grains.Grain.Ads;

public class AdsGain : Grain<AdsState>, IAdsGrain
{
    private readonly IObjectMapper _objectMapper;

    protected AdsGain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task CreateAsync(AdsIndex dto)
    {
        State = _objectMapper.Map<AdsIndex, AdsState>(dto);
        await WriteStateAsync();
    }

    public async Task UpdateAsync(AdsIndex dto)
    {
        State.VisitCount--;
        await WriteStateAsync();
    }

    public Task<AdsDto> GetAsync()
    {
        return Task.FromResult(_objectMapper.Map<AdsState, AdsDto>(State));
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }
}