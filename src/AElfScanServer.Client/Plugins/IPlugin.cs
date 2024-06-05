using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AElfScanServer.Plugins.Core.Plugins;

public interface IPlugin
{
    void ConfigureServices(IServiceCollection services);
    
    void Configure(IApplicationBuilder app, IWebHostEnvironment env);
    
}