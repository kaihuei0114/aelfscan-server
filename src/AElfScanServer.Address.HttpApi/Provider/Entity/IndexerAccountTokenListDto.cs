using System;
using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Provider.Entity;

public class IndexerAccountTokenListDto
{
    public List<AccountTokenDto> AccountToken { get; set; }
}