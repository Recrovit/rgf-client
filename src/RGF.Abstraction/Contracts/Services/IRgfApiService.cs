using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRgfApiService
{
    Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request) where ResultType : class;
    Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request) where ResultType : class;
}

public static class IRgfApiServiceExtension
{
    public static ApiRequest CreateRequest(this IRgfApiService service, string uri, HttpContent content = null, string query = null, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default, Version version = null, bool authClient = true)
        => new ApiRequest(uri, content, query, headerParam, cancellationToken, version, authClient);

    public static Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(this IRgfApiService service, string requestUri, string query = null, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default, Version version = null, bool authClient = true) where ResultType : class
        => service.GetAsync<ResultType>(service.CreateRequest(requestUri, null, query, headerParam, cancellationToken, version, authClient));

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(this IRgfApiService service, string requestUri, HttpContent content, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default, Version version = null, bool authClient = true) where ResultType : class
        => service.PostAsync<ResultType>(service.CreateRequest(requestUri, content, null, headerParam, cancellationToken, version, authClient));

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(this IRgfApiService service, string requestUri, string content, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default) where ResultType : class
        => service.PostAsync<ResultType>(requestUri, new StringContent(content, Encoding.UTF8, MediaTypeNames.Text.Plain), headerParam, cancellationToken);

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType, ContentType>(this IRgfApiService service, string requestUri, ContentType content, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default) where ResultType : class
        => service.PostAsync<ResultType, ContentType>(requestUri, content, new JsonSerializerOptions(JsonSerializerDefaults.Web), headerParam, cancellationToken);

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType, ContentType>(this IRgfApiService service, string requestUri, ContentType content, JsonSerializerOptions options, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default) where ResultType : class
    {
        StringContent json = new StringContent(JsonSerializer.Serialize(content, options), Encoding.UTF8, "application/json");
        return service.PostAsync<ResultType>(requestUri, json, headerParam, cancellationToken);
    }
}
