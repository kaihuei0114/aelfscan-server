using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using AElfScanServer.BFF.Core.Adaptor;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BFF.Core.Provider;

public interface IHttpRequestProvider
{
    Task<string> ExecuteRequestAsync(HttpDirective httpDirective);
}

public class HttpRequestProvider : IHttpRequestProvider, ITransientDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpRequestProvider> _logger;

    public HttpRequestProvider(IHttpClientFactory httpClientFactory, ILogger<HttpRequestProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await _httpClientFactory.CreateClient().GetStringAsync(url);
        return JsonConvert.DeserializeObject<T>(response);
    }

    public async Task<string> ExecuteRequestAsync(HttpDirective httpDirective)
    {
        var result = string.Empty;
        switch (httpDirective.Method)
        {
            case "GET":
                result = await ExecuteGetAsync(httpDirective);
                break;
            case "POST":
                result = await ExecutePostAsync(httpDirective);
                break;
            default:
                _logger.LogWarning("value type is null or undefined.");
                break;
        }

        return result;
    }

    private async Task<string> ExecutePostAsync(HttpDirective httpDirective)
    {
        if (httpDirective.RequestMediaType == "Form")
        {
            return await PostFormAsync(httpDirective.Url, httpDirective.Params, httpDirective.Headers);
        }

        return await PostJsonAsync(httpDirective.Url, httpDirective.Params, httpDirective.Headers);
    }

    private async Task<string> ExecuteGetAsync(HttpDirective httpDirective)
    {
        var client = _httpClientFactory.CreateClient();
        
        if (!httpDirective.Params.IsNullOrEmpty())
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var param in httpDirective.Params)
            {
                query[param.Key] = param.Value;
            }

            var queryString = query.ToString();
            var uriBuilder = new UriBuilder(httpDirective.Url)
            {
                Query = queryString
            };
            httpDirective.Url = uriBuilder.ToString();
        }

        if (httpDirective.Headers is not { Count: > 0 })
        {
            return await client.GetStringAsync(httpDirective.Url);
        }

        foreach (var keyValuePair in httpDirective.Headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        return await client.GetStringAsync(httpDirective.Url);
    }

    private async Task<string> PostJsonAsync(string url, Dictionary<string, string> param,
        Dictionary<string, string> headers)
    {
        var requestInput = param.IsNullOrEmpty() ? string.Empty : JsonConvert.SerializeObject(param, Formatting.None);

        var requestContent = new StringContent(
            requestInput,
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var client = _httpClientFactory.CreateClient();

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.PostAsync(url, requestContent);

        var content = await response.Content.ReadAsStringAsync();
        if (!ResponseSuccess(response.StatusCode))
        {
            _logger.LogError(
                "request fail, url:{url}, code:{code}, message: {message}, params:{param}",
                url, response.StatusCode, content, JsonConvert.SerializeObject(param));
            return null;
        }

        return content;
    }

    private async Task<string> PostFormAsync(string url, Dictionary<string, string> paramDic,
        Dictionary<string, string> headers)
    {
        var client = _httpClientFactory.CreateClient();

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var param = new List<KeyValuePair<string, string>>();
        if (paramDic is { Count: > 0 })
        {
            param.AddRange(paramDic.ToList());
        }

        var response = await client.PostAsync(url, new FormUrlEncodedContent(param));
        var content = await response.Content.ReadAsStringAsync();

        if (!ResponseSuccess(response.StatusCode))
        {
            _logger.LogError(
                "request fail, url:{url}, code:{code}, message: {message}, params:{param}",
                url, response.StatusCode, content, JsonConvert.SerializeObject(param));
            return null;
        }

        return content;
    }

    private static bool ResponseSuccess(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.OK or HttpStatusCode.NoContent;
}