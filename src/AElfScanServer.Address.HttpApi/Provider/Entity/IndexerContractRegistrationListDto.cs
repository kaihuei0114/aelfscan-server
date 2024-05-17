using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Provider.Entity;

public class IndexerContractRegistrationListDto
{
    public List<ContractRegistrationDto> ContractRegistration { get; set; }
}