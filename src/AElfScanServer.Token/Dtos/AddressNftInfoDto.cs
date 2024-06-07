using System;
using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Token.Dtos;

public class AddressNftInfoDto
{
    public TokenBaseInfo NftCollection { get; set; }
    public TokenBaseInfo Token { get; set; }
    public decimal Quantity { set; get; }
    public long TransferCount { get; set; }
    public DateTime? FirstNftTime { get; set; }
}