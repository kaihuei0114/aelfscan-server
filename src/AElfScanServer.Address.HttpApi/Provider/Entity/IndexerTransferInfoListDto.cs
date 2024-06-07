using System;
using System.Collections.Generic;

namespace AElfScanServer.Common.Address.HttpApi.Provider.Entity;

public class IndexerTransferInfoListDto
{
    public List<TransferInfoDto> TransferInfo { get; set; }
}