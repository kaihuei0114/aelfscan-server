using System.Collections.Generic;
using AElfScanServer.Dtos;
using AElfScanServer.Enums;

namespace AElfScanServer.Token.Dtos;

public class TokenTransferInfoDto
{
    public string ChainId { get; set; }
    public string TransactionId { get; set; }
    public string Method { get; set; }
    public long BlockHeight { get; set; }
    public long BlockTime { get; set; }
    public CommonAddressDto From { get; set; }
    public CommonAddressDto To { get; set; }
    public decimal Quantity { get; set; }
    
    public TransactionStatus Status { get; set; }
    
    public List<TransactionFeeDto> TransactionFeeList { get; set; }
}

public class TokenTransferInfosDto : ListResponseDto<TokenTransferInfoDto>
{
    public bool IsAddress { get; set; }
    public decimal Balance { get; set; }
    public decimal Value { get; set; }
}