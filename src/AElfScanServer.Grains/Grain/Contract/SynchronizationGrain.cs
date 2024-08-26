using AElfScanServer.Common.Dtos;
using AElfScanServer.Grains.State.Contract;
using Orleans.Providers;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.Grains.Grain.Contract;
[StorageProvider(ProviderName= "Default")]
public class SynchronizationGrain : Grain<SynchronizationState>, ISynchronizationGrain
{
    private readonly IObjectMapper _objectMapper;

    public SynchronizationGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }
    public async Task SaveAndUpdateAsync(SynchronizationDto synchronizationDtot)
    {
        State = _objectMapper.Map<SynchronizationDto,SynchronizationState>(synchronizationDtot);

        await WriteStateAsync();
    }

    public  async Task<SynchronizationDto> GetAsync()
    {
        if (State == null)
        {
            return new SynchronizationDto();
        }

        return _objectMapper.Map<SynchronizationState,SynchronizationDto>(State);
    }
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }
}