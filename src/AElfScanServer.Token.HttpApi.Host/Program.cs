using AElfScanServer.Token.HttpApi.Host;
using AElfScanServer.Token.HttpApi.Host.Extension;
using Serilog;
using Serilog.Events;
using Volo.Abp.Modularity.PlugIns;

namespace AElfScanServer.Token.HttpAp.Host;

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
            Log.Information("Starting TokenDataFunction Host.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("apollo.appsettings.json");
            builder.Host.AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseApollo()
                .UseSerilog();

            var combine = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");

            await builder.AddApplicationAsync<TokenHttpApiHostModule>(options =>
            {
                options.PlugInSources.AddFolder(combine);
            });
  
            string[] files = Directory.GetFiles(combine);
            Log.Information("Plugins path:{0}", combine);
            foreach (var file in files)
            {
                Log.Information("Plugins file name:{0}", file.ToString());
            }


            var app = builder.Build();
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