using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Provider.Entity;

public class IndexerAddressListDto
{
    public List<AccountInfoDto> AccountInfo { get; set; }
}