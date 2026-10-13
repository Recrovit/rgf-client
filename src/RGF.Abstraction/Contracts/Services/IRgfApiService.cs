using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

/// <summary>Provides typed GET and POST requests through the configured RGF HTTP clients.</summary>
public interface IRgfApiService
{
    /// <summary>Sends a GET request using its client, headers and cancellation settings.</summary>
    /// <typeparam name="ResultType">Reference type of the response payload.</typeparam>
    /// <param name="request">URI, optional content/query, additional headers and authentication-client choice.</param>
    /// <returns>HTTP envelope and typed payload; check Success before using Result.</returns>
    /// <remarks>The default client implementation reports HTTP, parsing and cancellation failures in the envelope.
    /// Payload-level workflow success is independent of HTTP success.</remarks>
    Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request) where ResultType : class;

    /// <summary>Sends a POST request using its client, headers and cancellation settings.</summary>
    /// <typeparam name="ResultType">Reference type of the response payload.</typeparam>
    /// <param name="request">URI, optional content/query, additional headers and authentication-client choice.</param>
    /// <returns>HTTP envelope and typed payload; check Success before using Result.</returns>
    /// <remarks>The default client implementation reports HTTP, parsing and cancellation failures in the envelope.
    /// Payload-level workflow success is independent of HTTP success.</remarks>
    Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request) where ResultType : class;
}

/// <summary>Creates API requests and adapts text or JSON bodies to the typed service contract.</summary>
public static class IRgfApiServiceExtension
{
    public static ApiRequest CreateRequest(this IRgfApiService service, string uri, HttpContent content = null, Dictionary<string, string> query = null, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default, bool authClient = true)
        => new ApiRequest(uri, content, query, additionalHeaders, cancellationToken, authClient);

    public static Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(this IRgfApiService service, string requestUri, Dictionary<string, string> query = null, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default, bool authClient = true) where ResultType : class
        => service.GetAsync<ResultType>(service.CreateRequest(requestUri, null, query, additionalHeaders, cancellationToken, authClient));

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(this IRgfApiService service, string requestUri, HttpContent content, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default, bool authClient = true) where ResultType : class
        => service.PostAsync<ResultType>(service.CreateRequest(requestUri, content, null, additionalHeaders, cancellationToken, authClient));

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(this IRgfApiService service, string requestUri, string content, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default) where ResultType : class
        => service.PostAsync<ResultType>(requestUri, new StringContent(content, Encoding.UTF8, MediaTypeNames.Text.Plain), additionalHeaders, cancellationToken);

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType, ContentType>(this IRgfApiService service, string requestUri, ContentType content, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default) where ResultType : class
        => service.PostAsync<ResultType, ContentType>(requestUri, content, new JsonSerializerOptions(JsonSerializerDefaults.Web), additionalHeaders, cancellationToken);

    public static Task<IRgfApiResponse<ResultType>> PostAsync<ResultType, ContentType>(this IRgfApiService service, string requestUri, ContentType content, JsonSerializerOptions options, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default) where ResultType : class
    {
        StringContent json = new StringContent(JsonSerializer.Serialize(content, options), Encoding.UTF8, "application/json");
        return service.PostAsync<ResultType>(requestUri, json, additionalHeaders, cancellationToken);
    }
}