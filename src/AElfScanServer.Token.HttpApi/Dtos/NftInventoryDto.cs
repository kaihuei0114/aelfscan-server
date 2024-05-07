using System.Collections.Generic;
using AElfScanServer.Token.Dtos;

namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftInventoryDto
{
    public TokenBaseInfo Item { get; set; }
    public decimal LastSalePriceInUsd { get; set; }
    public decimal LastSaleAmount { get; set; }
    public string LastTransactionId { get; set; }
}

public class NftInventorysDto : ListResponseDto<NftInventoryDto>
{
    public bool IsAddress { get; set; }
   
    public List<HolderInfo> Items { get; set; }
}

