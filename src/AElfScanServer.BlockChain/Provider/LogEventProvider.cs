using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AElfScanServer.BlockChain.Dtos;
using AElfScanServer.BlockChain.Helper;
using AElfScanServer.BlockChain.Options;
using AElfScanServer.Common.Options;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BlockChain.Provider;

public class LogEventProvider : AbpRedisCache, ISingletonDependency
{
    private readonly INESTRepository<LogEventIndex, string> _logEventIndexRepository;
    private readonly GlobalOptions _globalOptions;
    private readonly IElasticClient _elasticClient;


    private readonly ILogger<HomePageProvider> _logger;

    public LogEventProvider(
        ILogger<HomePageProvider> logger, IOptionsMonitor<GlobalOptions> blockChainOptions,
        IOptions<ElasticsearchOptions> options,
        INESTRepository<LogEventIndex, string> logEventIndexRepository,
        IOptions<RedisCacheOptions> optionsAccessor) : base(optionsAccessor)
    {
        _logger = logger;
        _globalOptions = blockChainOptions.CurrentValue;
        var uris = options.Value.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
        _logEventIndexRepository = logEventIndexRepository;
    }


    // public async Task<LogEventResponseDto> GetLogEventListAsync(GetLogEventRequestDto request)
    // {
    //     var result = new LogEventResponseDto();
    //
    //     try
    //     {
    //         var searchRequest =
    //             new SearchRequest(BlockChainIndexNameHelper.GenerateLogEventIndexName(request.ChainId))
    //             {
    //                 Query = new MatchAllQuery(),
    //                 Size = request.MaxResultCount,
    //                 // 设置每页返回的文档数量
    //                 Sort = new List<ISort>
    //                 {
    //                     new FieldSort() { Field = "blockHeight", Order = request.SortOrder },
    //                     new FieldSort { Field = "index", Order = SortOrder.Ascending }
    //                 },
    //             };
    //
    //
    //         if (request.BlockHeight > 0)
    //         {
    //             searchRequest.SearchAfter = new object[]
    //                 { request.BlockHeight, request.Index > 0 ? request.Index : 0 };
    //         }
    //
    //
    //         if (!request.ContractName.IsNullOrEmpty())
    //         {
    //             searchRequest.Query = new BoolQuery()
    //             {
    //                 Must = new List<QueryContainer>()
    //                 {
    //                     new TermQuery()
    //                     {
    //                         Field = "contractName",
    //                         Value = request.ContractName
    //                     },
    //                 }
    //             };
    //         }
    //
    //
    //         var searchResponse = _elasticClient.Search<LogEventIndex>(searchRequest);
    //         result.Total = searchResponse.Total;
    //
    //         var logEventIndices = searchResponse.Documents.ToList();
    //         result.LogEvents = logEventIndices;
    //     }
    //     catch (Exception e)
    //     {
    //         _logger.LogError(e, "GetLogEventList error,request:{@request}", request);
    //     }
    //
    //     return result;
    // }


    public async Task<LogEventResponseDto> GetLogEventListAsync(GetLogEventRequestDto request)
    {
        var result = new LogEventResponseDto();


        try
        {
            var mustQuery = new List<Func<QueryContainerDescriptor<LogEventIndex>, QueryContainer>>();


            mustQuery.Add(mu => mu.Term(t => t.Field(f => f.ContractAddress).Value(request.ContractAddress)));

            QueryContainer Filter(QueryContainerDescriptor<LogEventIndex> f) => f.Bool(b => b.Must(mustQuery));


            // var resp = await _logEventIndexRepository.GetListAsync(Filter, skip: request.SkipCount,
            //     limit: request.MaxResultCount,
            //     index: BlockChainIndexNameHelper.GenerateLogEventIndexName(request.ChainId));


            var resp = await _logEventIndexRepository.GetSortListAsync(Filter, skip: request.SkipCount,
                limit: request.MaxResultCount,
                index: BlockChainIndexNameHelper.GenerateLogEventIndexName(request.ChainId),
                sortFunc: GetQuerySortDescriptor(request.SortOrder));

            result.Total = resp.Item1;
            result.LogEvents = resp.Item2;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetLogEventList error,request:{@request}", request);
        }

        return result;
    }

    private static Func<SortDescriptor<LogEventIndex>, IPromise<IList<ISort>>> GetQuerySortDescriptor(SortOrder sort)
    {
        //use default
        var sortDescriptor = new SortDescriptor<LogEventIndex>();

        if (sort == SortOrder.Ascending)
        {
            sortDescriptor.Ascending(a => a.BlockHeight);
        }
        else
        {
            sortDescriptor.Descending(a => a.BlockHeight);
        }

        sortDescriptor.Ascending(a => a.Index);

        return _ => sortDescriptor;
    }
}