namespace AElfScanServer.Dtos;

public enum SymbolType
{
    Token,
    Nft,
    Nft_Collection
}
public enum AddressType
{
    EoaAddress,
    ContractAddress
}

public enum TransferStatusType
{
    In,
    Out
}

public class CommonAddressDto
{
    public string Name { get; set; }
    public string Address { get; set; }
    public AddressType AddressType { get; set; }
    public bool IsManager { get; set; }
    public bool IsProducer { get; set; }
}