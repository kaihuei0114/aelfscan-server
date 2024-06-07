using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Transactions;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.Address.HttpApi.Provider.Entity;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.Dtos;

namespace AElfScanServer.Common.Address.HttpApi.Dtos;

public class GetTransactionListInput : GetListInputBasicDto
{
    [Required] public string Address { get; set; }
}

public class GetTransactionListResultDto
{
    public long Total { get; set; }
    public List<TransactionResponseDto> Transactions { get; set; }
}

// public class TransactionDto
// {
//     public string TransactionId { get; set; }
//
//     public long BlockHeight { get; set; }
//
//     public string Method { get; set; }
//
//     public TransactionStatus Status { get; set; }
//     public CommonAddressDto From { get; set; }
//
//     public CommonAddressDto To { get; set; }
//
//     public long Timestamp { get; set; }
//
//     public string TransactionValue { get; set; }
//
//     public string TransactionFee { get; set; }
// }