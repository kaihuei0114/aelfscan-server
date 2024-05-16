using System.Collections.Concurrent;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.SchemaManager;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BFF.Core.Provider;

public interface IGraphQlExecutorProvider
{
    public Task<IRequestExecutor> GetExecutorAsync(string id);
}

public class GraphQlExecutorProvider : IGraphQlExecutorProvider, ISingletonDependency
{
    private readonly ISchemaManager _schemaManager;
    private readonly ConcurrentDictionary<string, IRequestExecutor> _executors;
    private readonly ILogger<GraphQlExecutorProvider> _logger;

    public GraphQlExecutorProvider(ISchemaManager schemaManager, ILogger<GraphQlExecutorProvider> logger)
    {
        _schemaManager = schemaManager;
        _executors = new ConcurrentDictionary<string, IRequestExecutor>();
        _logger = logger;
    }


    public async Task<IRequestExecutor> GetExecutorAsync(string id)
    {
        if (_executors.TryGetValue(id, out var executor))
        {
            return executor;
        }

        var schema = await _schemaManager.GetSchemaAsync(id);
        var newExecutor = schema.MakeExecutable();
        _executors[id] = newExecutor;
        return newExecutor;
    }
}