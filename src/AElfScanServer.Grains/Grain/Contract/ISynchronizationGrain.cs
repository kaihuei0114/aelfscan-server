using AElfScanServer.Common.Dtos;

namespace AElfScanServer.Grains.Grain.Contract;

public interface ISynchronizationGrain: IGrainWithStringKey
{
    Task SaveAndUpdateAsync(SynchronizationDto synchronizationDto);

    Task<SynchronizationDto> GetAsync();
}