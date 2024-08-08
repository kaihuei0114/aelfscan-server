using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.EntityMapping.Repositories;
using AElfScanServer.HttpApi.Provider;
using AElfScanServer.Common.Constant;
using AElfScanServer.Common.Core;
using AElfScanServer.Common.Dtos;
using AElfScanServer.Common.Dtos.ChartData;
using AElfScanServer.Common.Dtos.Indexer;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.HttpApi.Dtos.address;
using AElfScanServer.HttpApi.Dtos.Indexer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Ocsp;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.HttpApi.Service;

public interface IContractAppService
{
    Task<GetContractListResultDto> GetContractListAsync(GetContractContracts input);
    Task<GetContractFileResultDto> GetContractFileAsync(GetContractFileInput input);
    Task<GetContractHistoryResultDto> GetContractHistoryAsync(GetContractHistoryInput input);
    Task<GetContractEventResp> GetContractEventsAsync(GetContractEventReq input);
}

[Ump]
public class ContractAppService : IContractAppService
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<ContractAppService> _logger;
    private readonly IDecompilerProvider _decompilerProvider;
    private readonly IIndexerTokenProvider _indexerTokenProvider;
    private readonly IIndexerGenesisProvider _indexerGenesisProvider;
    private readonly IBlockChainIndexerProvider _blockChainIndexerProvider;
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly AELFIndexerProvider _aelfIndexerProvider;
    private readonly IEntityMappingRepository<LogEventIndex, string> _logEventIndexRepository;

    public ContractAppService(IObjectMapper objectMapper, ILogger<ContractAppService> logger,
        IDecompilerProvider decompilerProvider,
        IIndexerTokenProvider indexerTokenProvider, IIndexerGenesisProvider indexerGenesisProvider,
        IOptionsMonitor<GlobalOptions> globalOptions, IBlockChainIndexerProvider blockChainIndexerProvider,
        IEntityMappingRepository<LogEventIndex, string> logEventIndexRepository,
        AELFIndexerProvider aelfIndexerProvider)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _decompilerProvider = decompilerProvider;
        _indexerTokenProvider = indexerTokenProvider;
        _indexerGenesisProvider = indexerGenesisProvider;
        _globalOptions = globalOptions;
        _blockChainIndexerProvider = blockChainIndexerProvider;
        _logEventIndexRepository = logEventIndexRepository;
        _aelfIndexerProvider = aelfIndexerProvider;
    }

    public async Task<GetContractListResultDto> GetContractListAsync(GetContractContracts input)
    {
        _logger.LogInformation("GetContractListAsync");
        var result = new GetContractListResultDto { List = new List<ContractDto>() };

        var getContractListResult =
            await _indexerGenesisProvider.GetContractListAsync(input.ChainId,
                input.SkipCount,
                input.MaxResultCount, input.OrderBy, input.Sort, "");
        result.Total = getContractListResult.ContractList.TotalCount;


        var list = getContractListResult.ContractList.Items.Select(s => s.Address).ToList();

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


        foreach (var info in getContractListResult.ContractList.Items)
        {
            var blockBlockTime = info.Metadata.Block.BlockTime;
            if (info.Metadata.Block.BlockHeight == 1)
            {
                blockBlockTime = input.ChainId == "AELF"
                    ? CommonConstant.AELFOneBlockTime
                    : CommonConstant.TDVVOneBlockTime;
            }


            var contractInfo = new ContractDto
            {
                Address = info.Address,
                ContractVersion = info.ContractVersion == "" ? info.Version.ToString() : info.ContractVersion,
                LastUpdateTime = blockBlockTime,
                Type = info.ContractType,
                ContractName = GetContractName(input.ChainId, info.Address).Result
            };


            if (!addressTransactionCountList.IsNullOrEmpty() && addressTransactionCountList.Count > 0)
            {
                var countInfo =
                    addressTransactionCountList.Where(w => w.Address == info.Address).FirstOrDefault();

                contractInfo.Txns = countInfo == null ? 0 : countInfo.Count;
            }

            var addressTokenList = await _indexerTokenProvider.GetAddressTokenListAsync(input.ChainId, info.Address,
                "ELF", input.SkipCount, input.MaxResultCount);
            contractInfo.Balance = addressTokenList.Count > 0 ? addressTokenList[0].FormatAmount : 0;


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
        var contractInfo =
            await _indexerGenesisProvider.GetContractListAsync(input.ChainId, 0, 1, "", "", input.Address);
        if (contractInfo == null)
        {
            throw new UserFriendlyException("No contract info");
        }

        var getContractRegistrationResult =
            await _indexerGenesisProvider.GetContractRegistrationAsync(input.ChainId,
                contractInfo.ContractList.Items[0].CodeHash);

        if (getContractRegistrationResult.Count == 0)
        {
            throw new UserFriendlyException("No contract registration");
        }

        // todo get code from
        var getFilesResult = await _decompilerProvider.GetFilesAsync(getContractRegistrationResult[0].Code);
        return new GetContractFileResultDto
        {
            ContractName = await GetContractName(input.ChainId, input.Address),
            ContractVersion = getFilesResult.Version,
            ContractSourceCode = getFilesResult.Data
        };
    }

    public async Task<GetContractHistoryResultDto> GetContractHistoryAsync(
        GetContractHistoryInput input)
    {
        var result = new GetContractHistoryResultDto();
        var getContractRecordResult =
            await _indexerGenesisProvider.GetContractRecordAsync(input.ChainId, input.Address);

        var getContractListResult =
            await _indexerGenesisProvider.GetContractListAsync(input.ChainId,
                0,
                1, "", "", input.Address);

        var codeHash = getContractListResult.ContractList.Items.First().CodeHash;
        result.Record = getContractRecordResult.Select(t =>
        {
            var tempContractRecord = _objectMapper.Map<ContractInfoDto, ContractRecordDto>(t.ContractInfo);
            tempContractRecord.BlockTime = t.Metadata.Block.BlockTime;
            tempContractRecord.TransactionId = t.TransactionId;
            tempContractRecord.BlockHeight = t.Metadata.Block.BlockHeight;
            tempContractRecord.CodeHash = codeHash;
            tempContractRecord.Version = t.ContractInfo.ContractVersion;
            tempContractRecord.ContractOperationType = t.OperationType;
            return tempContractRecord;
        }).OrderByDescending(t => t.Version).ToList();

        return result;
    }

    public async Task<GetContractEventResp> GetContractEventsAsync(GetContractEventReq req)
    {
        var result = new GetContractEventResp()
        {
            List = new List<LogEventIndex>()
        };

        if (req.BlockHeight > 0)
        {
            var transactionList =
                await _aelfIndexerProvider.GetTransactionsAsync(req.ChainId, req.BlockHeight, req.BlockHeight, "");

            if (transactionList.IsNullOrEmpty())
            {
                return result;
            }

            for (var i = 0; i < transactionList.Count; i++)
            {
                var txn = transactionList[i];

                if (txn.To != req.ContractAddress)
                {
                    continue;
                }

                for (var i1 = 0; i1 < txn.LogEvents.Count; i1++)
                {
                    var curEvent = txn.LogEvents[i1];
                    curEvent.ExtraProperties.TryGetValue("Indexed", out var indexed);
                    curEvent.ExtraProperties.TryGetValue("NonIndexed", out var nonIndexed);
                    var logEvent = new LogEventIndex()
                    {
                        TransactionId = txn.TransactionId,
                        ChainId = req.ChainId,
                        BlockHeight = txn.BlockHeight,
                        MethodName = txn.MethodName,
                        BlockTime = txn.BlockTime,
                        TimeStamp = txn.BlockTime.ToUtcMilliSeconds(),
                        ToAddress = txn.To,
                        ContractAddress = curEvent.ContractAddress,
                        EventName = curEvent.EventName,
                        NonIndexed = nonIndexed,
                        Indexed = indexed,
                        Index = i1
                    };
                    result.List.Add(logEvent);
                }
            }

            result.List = result.List.Skip(req.SkipCount).Take(req.MaxResultCount).ToList();
            result.Total = result.List.Count;
            return result;
        }

        var queryable = _logEventIndexRepository.GetQueryableAsync().Result.Where(c => c.ChainId == req.ChainId)
            .Where(c => c.ToAddress == req.ContractAddress);

        var count = queryable.Count();
        var logEventIndices = queryable.Skip(req.SkipCount).Take(req.MaxResultCount).OrderByDescending(c => c.BlockTime)
            .ToList();

        result.Total = count > 10000 ? 10000 : count;
        result.List = logEventIndices;


        return result;
    }
}