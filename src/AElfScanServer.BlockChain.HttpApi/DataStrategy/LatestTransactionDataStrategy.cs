using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.DataStrategy;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AElfScanServer.BlockChain.HttpApi.DataStrategy;

public class LatestTransactionDataStrategy : DataStrategyBase<string, TransactionsResponseDto>
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;


    public LatestTransactionDataStrategy(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<DataStrategyBase<string, TransactionsResponseDto>> logger,
        IOptionsMonitor<GlobalOptions> globalOptions,
        IBlockChainIndexerProvider blockChainIndexerProvider) : base(optionsAccessor, logger)
    {
        _globalOptions = globalOptions;
        _blockChainIndexerProvider = blockChainIndexerProvider;
    }

    public override async Task<TransactionsResponseDto> QueryData(string chainId)
    {
        var result = new TransactionsResponseDto();
        result.Transactions = new List<TransactionResponseDto>();

        try
        {
            var indexerTransactionList = await _blockChainIndexerProvider.GetTransactionsAsync(chainId,
                0, 6, 0, 0, "");


            foreach (var transactionIndex in indexerTransactionList.Items)
            {
                var transactionRespDto = new TransactionResponseDto()
                {
                    TransactionId = transactionIndex.TransactionId,
                    Timestamp = DateTimeHelper.GetTotalSeconds(transactionIndex.Metadata.Block.BlockTime),
                    TransactionValue = transactionIndex.TransactionValue.ToString(),
                    BlockHeight = transactionIndex.BlockHeight,
                    Method = transactionIndex.MethodName,
                    Status = transactionIndex.Status,
                    TransactionFee = transactionIndex.Fee.ToString(),
                    From = new CommonAddressDto() { Address = transactionIndex.From },
                    To = new CommonAddressDto() { Address = transactionIndex.To }
                };


                if (_globalOptions.CurrentValue.ContractNames.TryGetValue(chainId, out var contractNames))
                {
                    if (contractNames.TryGetValue(transactionIndex.From, out var contractName))
                    {
                        transactionRespDto.From.Name = contractName;
                    }

                    if (contractNames.TryGetValue(transactionIndex.To, out contractName))
                    {
                        transactionRespDto.To.Name = contractName;
                    }
                }


                result.Transactions.Add(transactionRespDto);
            }
        }
        catch (Exception e)
        {
            DataStrategyLogger.LogError(e, "GetLatestTransactionsAsync error");
        }

        return result;
    }

    public override string DisplayKey(string chainId)
    {
        return RedisKeyHelper.LatestTransactions(chainId);
    }
}