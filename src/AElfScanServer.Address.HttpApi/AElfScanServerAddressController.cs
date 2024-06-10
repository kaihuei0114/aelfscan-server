using Volo.Abp.AspNetCore.Mvc;

namespace AElfScanServer.Address.HttpApi;

public abstract class AElfScanServerAddressController : AbpControllerBase
{
    protected AElfScanServerAddressController()
    {
        LocalizationResource = typeof(AddressServerResource);
    }
}