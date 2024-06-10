using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.Indexer;


namespace AElfScanServer.Common.Address.HttpApi.Provider.Entity;

public class ContractRecordDto : GraphQLDto
{
    public string OperationType { get; set; }
    public string Operator { get; set; }
    public string TransactionId { get; set; }
    public ContractInfoDto ContractInfo { get; set; }
    public MetadataDto Metadata { get; set; }
}

public enum ContractOperationType
{
    DeployContract,
    UpdateContract,
    SetAuthor
}