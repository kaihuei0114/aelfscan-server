using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.Address.HttpApi.Dtos;
using AElfScanServer.Address.HttpApi.Provider;
using AElfScanServer.BlockChain;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Dtos.Indexer;
using AElfScanServer.BlockChain.Provider;
using AElfScanServer.Core;
using AElfScanServer.Dtos.Indexer;
using AElfScanServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.Address.HttpApi.AppServices;

public interface IContractAppService
{
    Task<GetContractListResultDto> GetContractListAsync(GetContractContracts input);
    Task<GetContractFileResultDto> GetContractFileAsync(GetContractFileInput input);
    Task<GetContractHistoryResultDto> GetContractHistoryAsync(GetContractHistoryInput input);
    Task<GetContractEventListResultDto> GetContractEventsAsync(GetContractEventContractsInput input);
}

[Ump]
public class ContractAppService : IContractAppService
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<ContractAppService> _logger;
    private readonly IDecompilerProvider _decompilerProvider;
    private readonly IBlockChainProvider _blockChainProvider;
    private readonly IIndexerTokenProvider _indexerTokenProvider;
    private readonly IIndexerGenesisProvider _indexerGenesisProvider;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;

    public ContractAppService(IObjectMapper objectMapper, ILogger<ContractAppService> logger,
        IDecompilerProvider decompilerProvider, IBlockChainProvider blockChainProvider,
        IIndexerTokenProvider indexerTokenProvider, IIndexerGenesisProvider indexerGenesisProvider,
        IOptionsMonitor<GlobalOptions> globalOptions, IBlockChainIndexerProvider blockChainIndexerProvider)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _decompilerProvider = decompilerProvider;
        _blockChainProvider = blockChainProvider;
        _indexerTokenProvider = indexerTokenProvider;
        _indexerGenesisProvider = indexerGenesisProvider;
        _globalOptions = globalOptions;
        _blockChainIndexerProvider = blockChainIndexerProvider;
    }

    public async Task<GetContractListResultDto> GetContractListAsync(GetContractContracts input)
    {
        _logger.LogInformation("GetContractListAsync");
        var result = new GetContractListResultDto { List = new List<ContractDto>() };

        // todo sort by update time
        var getContractListResult =
            await _indexerGenesisProvider.GetContractListAsync(input.ChainId, input.SkipCount, input.MaxResultCount);
        result.Total = getContractListResult.Count;


        var list = getContractListResult.Select(s => s.Address).ToList();

        var addressTransactionCountList = new List<IndexerAddressTransactionCountDto>();

        try
        {
            addressTransactionCountList =
                await _blockChainIndexerProvider.GetAddressTransactionCount(input.ChainId, list);
        }
        catch (Exception e)
        {
            _logger.LogError("Query address transaction count err:{e}", e);
        }


        foreach (var info in getContractListResult)
        {
            var contractInfo = new ContractDto
            {
                Address = info.Address, // contractInfo
                ContractVersion = info.ContractVersion == "" ? info.Version.ToString() : info.ContractVersion,
                LastUpdateTime = info.Metadata.Block.BlockTime,
                Type = info.ContractType,
                Txns = 0,
                ContractName = GetContractName(input.ChainId, info.Address).Result
            };

            if (!addressTransactionCountList.IsNullOrEmpty() && addressTransactionCountList.Count > 0)
            {
                var countInfo =
                    addressTransactionCountList.Where(w => w.Address == info.Address).FirstOrDefault();

                contractInfo.Txns = countInfo == null ? 0 : countInfo.Count;
            }

            // todo: support batch search by address list.
            var addressTokenList = await _indexerTokenProvider.GetAddressTokenListAsync(input.ChainId, info.Address,
                "ELF", input.SkipCount, input.MaxResultCount);
            contractInfo.Balance = addressTokenList.Count > 0 ? addressTokenList[0].FormatAmount : 0;

            contractInfo.Txns = addressTokenList.Count > 0 ? addressTokenList[0].TransferCount : 0;

            result.List.Add(contractInfo);
        }


        return result;
    }


    public async Task<string> GetContractName(string chainId, string address)
    {
        _globalOptions.CurrentValue.ContractNames.TryGetValue(chainId, out var contractNames);
        if (contractNames == null)
        {
            return "";
        }

        contractNames.TryGetValue(address, out var contractName);

        return contractName;
    }


    public async Task<GetContractFileResultDto> GetContractFileAsync(GetContractFileInput input)
    {
        _logger.LogInformation("GetContractFileAsync");
        var contractInfo = await _indexerGenesisProvider.GetContractAsync(input.ChainId, input.Address);
        var getContractRegistrationResult =
            await _indexerGenesisProvider.GetContractRegistrationAsync(input.ChainId, contractInfo.CodeHash);

        if (getContractRegistrationResult.Count == 0)
        {
            throw new UserFriendlyException("No contract registration");
        }

        // todo get code from
        var getFilesResult = await _decompilerProvider.GetFilesAsync(getContractRegistrationResult[0].Code);
        return new GetContractFileResultDto
        {
            ContractName = "ContractName", // todo blockChain provider
            ContractVersion = getFilesResult.Version,
            ContractSourceCode = getFilesResult.Data
        };
    }

    public async Task<GetContractHistoryResultDto> GetContractHistoryAsync(
        GetContractHistoryInput input)
    {
        _logger.LogInformation("GetContractHistoryAsync");

        var result = new GetContractHistoryResultDto();
        var getContractRecordResult =
            await _indexerGenesisProvider.GetContractRecordAsync(input.ChainId, input.Address);

        result.Record = getContractRecordResult.Select(t =>
        {
            var tempContractRecord = _objectMapper.Map<ContractInfoDto, ContractRecordDto>(t.ContractInfo);
            tempContractRecord.BlockTime = t.Metadata.Block.BlockTime;
            tempContractRecord.TransactionId = t.TransactionId;
            tempContractRecord.BlockHeight = t.Metadata.Block.BlockHeight;
            tempContractRecord.ContractOperationType = t.OperationType;
            return tempContractRecord;
        }).OrderByDescending(t => t.Version).ToList();

        return result;
    }

    public async Task<GetContractEventListResultDto> GetContractEventsAsync(GetContractEventContractsInput input)
        => _objectMapper.Map<LogEventResponseDto, GetContractEventListResultDto>(
            await _blockChainProvider.GetLogEventListAsync(input.ChainId, input.Address, input.SkipCount,
                input.MaxResultCount));
}