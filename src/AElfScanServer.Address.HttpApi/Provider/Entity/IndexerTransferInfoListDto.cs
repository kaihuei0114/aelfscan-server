using System;
using System.Collections.Generic;

namespace AElfScanServer.Address.HttpApi.Provider.Entity;

public class IndexerTransferInfoListDto
{
    public List<TransferInfoDto> TransferInfo { get; set; }
}