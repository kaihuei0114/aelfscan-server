using System;
using System.Threading.Tasks;
using AElfScanServer.BlockChainDataFunction.Extension;
using AElfScanServer.BlockChainDataFunction.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace AElfScanServer.BlockChainDataFunction;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)

#if DEBUG
            .WriteTo.Async(c => c.Console())
#endif
            .CreateLogger();

        try
        {
            Log.Information("Starting BlockChainDataFunction Host.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("apollo.appsettings.json");
            builder.Host.AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseApollo()
                .UseSerilog();
            builder.Services.AddSignalR();
            await builder.AddApplicationAsync<BlockChainHttpApiHostModule>();
           
            var app = builder.Build();
            app.MapHub<ExploreHub>("api/app/blockchain/explore");
            
            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}