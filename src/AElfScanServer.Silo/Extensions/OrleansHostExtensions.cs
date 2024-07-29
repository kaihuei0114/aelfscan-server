using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Serialization;
using Orleans.Statistics;
using Orleans.Streams.Kafka.Config;

namespace AElfScanServer.Silo.Extensions;

public static class OrleansHostExtensions
{
    public static IHostBuilder UseOrleansSnapshot(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseOrleans((context, siloBuilder) =>
        {
            var configSection = context.Configuration.GetSection("Orleans");
            if (configSection == null)
                throw new ArgumentNullException(nameof(configSection), "The OrleansServer node is missing");
            var isRunningInKubernetes = configSection.GetValue<bool>("IsRunningInKubernetes");
            var advertisedIP = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("POD_IP")
                : configSection.GetValue<string>("AdvertisedIP");
            var clusterId = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID")
                : configSection.GetValue<string>("ClusterId");
            var serviceId = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID")
                : configSection.GetValue<string>("ServiceId");
            siloBuilder
                .ConfigureEndpoints(advertisedIP: IPAddress.Parse(advertisedIP),
                    siloPort: configSection.GetValue<int>("SiloPort"),
                    gatewayPort: configSection.GetValue<int>("GatewayPort"), listenOnAnyHostAddress: true)
                .UseMongoDBClient(configSection.GetValue<string>("MongoDBClient"))
                .UseMongoDBClustering(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                })
                .Configure<JsonGrainStateSerializerOptions>(options => options.ConfigureJsonSerializerSettings =
                    settings =>
                    {
                        settings.NullValueHandling = NullValueHandling.Include;
                        settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                        settings.DefaultValueHandling = DefaultValueHandling.Populate;
                    })
                .ConfigureServices(services =>
                    services.AddSingleton<IGrainStateSerializer, AeFinderJsonGrainStateSerializer>())
                .AddMemoryGrainStorage("OrleansStorage")
                .AddMemoryGrainStorageAsDefault()
                .Configure<GrainCollectionOptions>(options =>
                {
                    // Override the value of CollectionAge to
                    var collection = configSection.GetSection(nameof(GrainCollectionOptions.ClassSpecificCollectionAge))
                        .GetChildren();
                    foreach (var item in collection)
                    {
                        options.ClassSpecificCollectionAge[item.Key] = TimeSpan.FromSeconds(int.Parse(item.Value));
                    }
                })
                // .Configure<GrainCollectionNameOptions>(options =>
                // {
                //     var collectionName = configSection
                //         .GetSection(nameof(GrainCollectionNameOptions.GrainSpecificCollectionName)).GetChildren();
                //     options.GrainSpecificCollectionName = collectionName.ToDictionary(o => o.Key, o => o.Value);
                // })
                .UseMongoDBReminders(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    options.CreateShardKeyForCosmos = false;
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = clusterId;
                    options.ServiceId = serviceId;
                })
                .Configure<SiloMessagingOptions>(options =>
                {
                    options.ResponseTimeout = TimeSpan.FromSeconds(configSection.GetValue<int>("GrainResponseTimeOut"));
                    options.MaxMessageBodySize = configSection.GetValue<int>("GrainMaxMessageBodySize");
                    options.MaxForwardCount = configSection.GetValue<int>("MaxForwardCount");
                })
                // .AddAeFinderMongoDBGrainStorage("PubSubStore", options =>
                // {
                //     // Config PubSubStore Storage for Persistent Stream 
                //     options.CollectionPrefix = "StreamStorage";
                //     options.DatabaseName = configSection.GetValue<string>("DataBase");
                // })
                .Configure<ExceptionSerializationOptions>(options =>
                {
                    options.SupportedNamespacePrefixes.Add("Volo.Abp");
                    options.SupportedNamespacePrefixes.Add("Newtonsoft.Json");
                })
                .UseDashboard(options =>
                {
                    options.Username = configSection.GetValue<string>("DashboardUserName");
                    options.Password = configSection.GetValue<string>("DashboardPassword");
                    options.Host = "*";
                    options.Port = configSection.GetValue<int>("DashboardPort");
                    options.HostSelf = true;
                    options.CounterUpdateIntervalMs = configSection.GetValue<int>("DashboardCounterUpdateIntervalMs");
                })
                .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); });
        });
    }
}