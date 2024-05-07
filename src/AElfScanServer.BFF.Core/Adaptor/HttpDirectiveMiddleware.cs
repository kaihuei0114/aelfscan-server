using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.Provider;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.AspNetCore.Mvc;

namespace AElfScanServer.BFF.Core.Adaptor;

public class HttpDirectiveMiddleware
{
    private readonly FieldDelegate _next;
    private readonly IHttpRequestProvider _httpRequestProvider;

    public HttpDirectiveMiddleware(FieldDelegate next, [FromServices] IHttpRequestProvider httpRequestProvider)
    {
        _next = next;
        _httpRequestProvider = httpRequestProvider;
    }

    public async Task InvokeAsync(IMiddlewareContext context, HttpDirective directive)
    {
        var service = context.Services.GetService(typeof(HttpRequestProvider));
        var dataLoader = context.BatchDataLoader<HttpDirective, object>(GetResultsAsync, "GetResultsByHttpRequest");
        var response = await dataLoader.LoadAsync(directive);
        context.Result = response;
        await _next.Invoke(context);
    }

    private async Task<IReadOnlyDictionary<HttpDirective, object>> GetResultsAsync(IReadOnlyList<HttpDirective> keys,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<HttpDirective, object>();
        foreach (var item in keys)
        {
            if (results.ContainsKey(item))
            {
                continue;
            }

            var result = await _httpRequestProvider.ExecuteRequestAsync(item);
            if (string.IsNullOrEmpty(result))
            {
                continue;
            }

            var json = JsonDocument.Parse(result).RootElement;
            if (!string.IsNullOrWhiteSpace(item.Path))
            {
                var splits = item.Path.Split(".");
                json = splits.Aggregate(json, (current, sub) => current.GetProperty(sub));
            }

            if (json.ValueKind is JsonValueKind.Array)
            {
                results.Add(item, JsonSerializer.Deserialize<object[]>(json.GetRawText()));
            }
            else
            {
                results.Add(item, json);
            }
        }

        return results;
    }
}