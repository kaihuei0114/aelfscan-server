using System;
using AElfScanServer.Dtos;
using AElfScanServer.Enums;

namespace AElfScanServer.Common.Token.HttpApi.Dtos;

public class NftItemActivityDto
{
    public string TransactionId { get; set; }
    public TransactionStatus Status { get; set; }
    public string Action { get; set; }
    public DateTime BlockTime { get; set; }
    public long BlockHeight { get; set; }
    public decimal PriceOfUsd { get; set; }
    public decimal Price { get; set; }
    public string PriceSymbol { get; set; }
    public decimal Quantity { get; set; }
    public CommonAddressDto From { get; set; }
    public CommonAddressDto To { get; set; }
}