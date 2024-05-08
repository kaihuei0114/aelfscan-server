namespace AElfScanServer.TokenDataFunction.Dtos;

public class NftSalInfoDto
{
    public decimal SaleAmount { get; set; }
    
    public string SaleAmountSymbol { get; set; }
    public string TransactionId { get; set; }
    
    public long BlockHeight { get; set; }
}