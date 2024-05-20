using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.Provider;
using HotChocolate.AspNetCore.Serialization;
using HotChocolate.Execution;
using HotChocolate.Language;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RequestDelegate = Microsoft.AspNetCore.Http.RequestDelegate;

namespace AElfScanServer.BFF.Core.RequestHandler;

public class CustomHttpPostMiddlewareBase
{ 
    private const string ApiPathPrefix = "/api/app";
    private readonly RequestDelegate _next;
    private readonly IHttpRequestParser _requestParser;
    private readonly ILogger<CustomHttpPostMiddlewareBase> _logger;

    public CustomHttpPostMiddlewareBase(RequestDelegate next, IHttpRequestParser requestParser,
        ILogger<CustomHttpPostMiddlewareBase> logger)
    {
        _next = next;
        _requestParser = requestParser;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IGraphQlExecutorProvider graphQlExecutorProvider)
    {
        var bffResult = new BffResult();
        context.Response.ContentType = "application/json";
        try
        {
            var requestPath = context.Request.Path;
            if (requestPath.StartsWithSegments(ApiPathPrefix))
            {
                var route = GetRequestRoute(requestPath.Value);
                var executor = await graphQlExecutorProvider.GetExecutorAsync(route);

                var query = await ReadQueryStringAsync(context.Request, context.RequestAborted);
                var queryRequest = QueryRequestBuilder.From(query).Create();
                var result = await executor.ExecuteAsync(queryRequest);
                var readOnlyDictionary = ((QueryResult)result).Data;
                var data = readOnlyDictionary?.Values.FirstOrDefault();
                bffResult = BffResult.Success(data);
            }
            else
            {
                await _next(context);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "execute query failed.");
            bffResult = BffResult.Failure(e.Message);
        }

        await context.Response.WriteAsync(JsonConvert.SerializeObject(bffResult));
    }

    private static string GetRequestRoute(string requestPath)
    {
        if (string.IsNullOrEmpty(requestPath))
        {
            throw new Exception("the query route is invalid.");
        }
        var remainingUrl = requestPath.Substring((ApiPathPrefix + '/').Length);
        var segments = remainingUrl.Split('/');

        if (segments.Length > 0)
        {
            return segments[0];
        }
        throw new Exception("the query route is invalid.");
    }

    private async ValueTask<GraphQLRequest> ReadQueryStringAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var queryList = await _requestParser.ReadJsonRequestAsync(request.Body, cancellationToken);
        if (queryList.Count != 1)
        {
            throw new Exception("the query syntax is incorrect.");
        }

        return queryList[0];
    }
}