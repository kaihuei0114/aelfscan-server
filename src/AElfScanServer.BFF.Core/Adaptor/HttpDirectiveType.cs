using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.Provider;
using HotChocolate.Types;

namespace AElfScanServer.Common.BFF.Core.Adaptor;

public class HttpDirectiveType : DirectiveType<HttpDirective>
{
    private readonly IHttpRequestProvider _httpRequestProvider;

    public HttpDirectiveType(IHttpRequestProvider httpRequestProvider)
    {
        _httpRequestProvider = httpRequestProvider;
    }

    protected override void Configure(IDirectiveTypeDescriptor<HttpDirective> descriptor)
    {
        descriptor.Name(DirectiveConstant.CustomHttpDirectiveName);
        descriptor.Location(DirectiveLocation.Object | DirectiveLocation.FieldDefinition);
        //descriptor.Use<HttpDirectiveMiddleware>();
        descriptor.Use((next, directive) => async context =>
        {
            var dataLoader = context.BatchDataLoader<HttpDirective, object>(GetResultsAsync, "GetResultsByHttpRequest");

            var httpDirective = directive.AsValue<HttpDirective>();
            var paramMap = new Dictionary<string, string>();
            foreach (var arg in context.Selection.Arguments)
            {
                paramMap[arg.Name] = arg.Value == null ? string.Empty : arg.Value.ToString();
            }

            httpDirective.Params = paramMap;
            var response = await dataLoader.LoadAsync(httpDirective);
            context.Result = response;
            await next.Invoke(context);
        });
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