using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Token.Dtos;

public class TokenHolderInfoDto
{
    public CommonAddressDto Address { get; set; }
    public decimal Quantity { get; set; }
    public decimal Percentage { get; set; }
    public decimal Value { get; set; }
}

public class AccountCountInput
{
    public string ChainId { get; set; }
}

public class AccountCountResultDto
{
    public AccountCountDto AccountCount { get; set; }
}

public class AccountCountDto
{
    public int Count { get; set; }
}