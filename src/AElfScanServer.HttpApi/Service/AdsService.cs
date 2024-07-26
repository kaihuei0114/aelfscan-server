using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AeFinder.Grains;
using AElf.EntityMapping.Repositories;
using AElfScanServer.Common.Dtos.Ads;
using AElfScanServer.Common.Helper;
using AElfScanServer.Common.Options;
using AElfScanServer.Grains.Grain.Ads;
using AElfScanServer.Grains.State.Ads;
using AElfScanServer.HttpApi.Dtos.AdsData;
using AElfScanServer.HttpApi.Options;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nest;
using Orleans;
using Volo.Abp;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AElfScanServer.HttpApi.Service;

public interface IAdsService
{
    public Task<AdsResp> GetAds(AdsReq req);

    public Task<AdsIndex> UpdateAds(UpdateAdsReq req);

    public Task<AdsIndex> DeleteAds(DeleteAdsReq req);


    public Task<AdsListResp> GetAdsList(GetAdsListReq req);
}

public class AdsService : AbpRedisCache, IAdsService, ITransientDependency
{
    private readonly IOptionsMonitor<GlobalOptions> _globalOptions;
    private readonly ILogger<AdsService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IEntityMappingRepository<AdsIndex, string> _adsRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly IElasticClient _elasticClient;

    public AdsService(IOptions<RedisCacheOptions> optionsAccessor, IOptionsMonitor<GlobalOptions> globalOptions,
        ILogger<AdsService> logger,
        IClusterClient clusterClient, IEntityMappingRepository<AdsIndex, string> adsRepository,
        IObjectMapper objectMapper, IOptionsMonitor<ElasticsearchOptions> options) : base(
        optionsAccessor)
    {
        _globalOptions = globalOptions;
        _logger = logger;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _adsRepository = adsRepository;
        var uris = options.CurrentValue.Url.ConvertAll(x => new Uri(x));
        var connectionPool = new StaticConnectionPool(uris);
        var settings = new ConnectionSettings(connectionPool);
        _elasticClient = new ElasticClient(settings);
    }


    public async Task<AdsListResp> GetAdsList(GetAdsListReq req)
    {
        var result = new AdsListResp();
        var searchResponse = _elasticClient.Search<AdsIndex>(s => s
            .Index("adsindex")
            .Query(q => q
                .Bool(b => b
                    .Must(m =>
                    {
                        if (req.Labels == null || !req.Labels.Any())
                            return m.MatchAll();
                        else
                            return m.Terms(t => t
                                .Field(f => f.Labels.Suffix("keyword"))
                                .Terms(req.Labels)
                            );
                    })
                )
            )
            .Sort(sort => sort
                .Field(f => f
                    .Field(c => c.CreateTime)
                    .Order(SortOrder.Descending)
                )
            )
            .Size(1000)
        );

        var adsIndexes = searchResponse.Documents.ToList();
        result.List = adsIndexes;
        result.Total = adsIndexes.Count;
        return result;
    }

    public async Task<AdsResp> GetAds(AdsReq req)
    {
        var dateStr = DateTimeHelper.GetDateStr(DateTime.UtcNow);
        var key = GrainIdHelper.GenerateAdsKey(req.Ip, req.Device, req.Label, dateStr);
        await ConnectAsync();
        var adsVisitCount = RedisDatabase.StringGet(key);
        var adsList = new List<AdsIndex>();
        var adsResp = new AdsResp();

        adsList = await QueryAdsList(req.Label, "", 1000);
        if (adsList.IsNullOrEmpty())
        {
            return adsResp;
        }

        if (adsVisitCount.IsNullOrEmpty)
        {
            var adsIndex = adsList.First();
            adsResp = _objectMapper.Map<AdsIndex, AdsResp>(adsIndex);

            RedisDatabase.StringIncrement(key);
            RedisDatabase.KeyExpire(key, TimeSpan.FromDays(7));
            return adsResp;
        }

        var count = long.Parse(adsVisitCount);

        var totalVisitCount = 0;
        foreach (var ads in adsList)
        {
            totalVisitCount += ads.TotalVisitCount;
            if (count < totalVisitCount)
            {
                RedisDatabase.StringIncrement(key);
                return _objectMapper.Map<AdsIndex, AdsResp>(ads);
            }
        }


        RedisDatabase.StringSet(key, 1);
        return _objectMapper.Map<AdsIndex, AdsResp>(adsList.First());
    }


    public async Task<List<AdsIndex>> QueryAdsList(string label, string adsId, int size)
    {
        var utcMilliSeconds = DateTime.UtcNow.ToUtcMilliSeconds();
        var searchResponse = _elasticClient.Search<AdsIndex>(s => s
            .Index("adsindex")
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m =>
                        {
                            if (label.IsNullOrEmpty())
                                return m.MatchAll();
                            return m.Terms(t => t
                                .Field(f => f.Labels.Suffix("keyword"))
                                .Terms(label)
                            );
                        },
                        m => m.Range(r => r
                            .Field(f => f.StartTime)
                            .LessThanOrEquals(utcMilliSeconds)
                        ),
                        m => m.Range(r => r
                            .Field(f => f.EndTime)
                            .GreaterThanOrEquals(utcMilliSeconds)
                        ),
                        m =>
                        {
                            if (adsId.IsNullOrEmpty())
                                return m.MatchAll();
                            return m.Term(t => t
                                .Field(f => f.AdsId)
                                .Value(adsId)
                            );
                        }
                    )
                )
            ).Sort(sort => sort
                .Field(f => f
                    .Field(c => c.CreateTime)
                    .Order(SortOrder.Ascending)
                )
            )
            .Size(size)
        );

        return searchResponse.Documents.ToList();
    }

    public async Task<AdsIndex> UpdateAds(UpdateAdsReq req)
    {
        var adsIndex = _objectMapper.Map<UpdateAdsReq, AdsIndex>(req);
        if (req.AdsId.IsNullOrEmpty())
        {
            var adsId = Guid.NewGuid().ToString();
            adsIndex.AdsId = adsId;
            adsIndex.Id = adsId;
            adsIndex.CreateTime = DateTime.UtcNow;
            adsIndex.UpdateTime = DateTime.UtcNow;
        }
        else
        {
            adsIndex.Id = adsIndex.AdsId;
        }


        adsIndex.UpdateTime = DateTime.UtcNow;
        await _adsRepository.AddOrUpdateAsync(adsIndex);
        return adsIndex;
    }

    public async Task<AdsIndex> DeleteAds(DeleteAdsReq req)
    {
        var queryableAsync = await _adsRepository.GetQueryableAsync();
        var adsIndices = queryableAsync.Where(c => c.AdsId == req.AdsId).Take(1);

        if (adsIndices.IsNullOrEmpty())
        {
            return new AdsIndex();
        }

        var index = new AdsIndex()
        {
            Id = req.AdsId
        };
        await _adsRepository.DeleteAsync(index);

        return adsIndices.First();
    }
}