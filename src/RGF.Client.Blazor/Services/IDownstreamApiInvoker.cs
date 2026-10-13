using System.Net.Http.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

/// <summary>
/// Sends requests to named downstream APIs without exposing the current Blazor runtime mode.
/// </summary>
public interface IDownstreamApiInvoker
{
    /// <summary>Sends a request through the registered host-session transport.</summary>
    /// <param name="request">Endpoint, method, optional content and headers.</param>
    /// <param name="cancellationToken">Cancels HTTP sending.</param>
    /// <returns>The response with headers read; the caller owns disposal and status handling.</returns>
    Task<HttpResponseMessage> SendAsync(DownstreamApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a request to a configured downstream API.
/// </summary>
public sealed class DownstreamApiRequest
{
    /// <summary>Nonblank configured downstream alias; the SessionAuth adapter escapes it into the /downstream route.</summary>
    public required string ApiName { get; init; }

    /// <summary>Downstream path appended after the API alias; leading slashes are removed and query text can be included.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>HTTP method; defaults to GET.</summary>
    public HttpMethod Method { get; init; } = HttpMethod.Get;

    /// <summary>Optional body transferred to the outgoing request; do not reuse it after sending.</summary>
    public HttpContent? Content { get; init; }

    /// <summary>Optional request headers, with content-header fallback when a body is present.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Headers { get; init; }
}

/// <summary>
/// Convenience helpers for downstream API requests.
/// </summary>
public static class DownstreamApiInvokerExtensions
{
    /// <summary>Sends GET through this invoker; the caller owns response disposal and status handling.</summary>
    public static Task<HttpResponseMessage> GetAsync(
        this IDownstreamApiInvoker invoker,
        string apiName,
        string relativePath = "",
        CancellationToken cancellationToken = default)
    {
        return invoker.SendAsync(new DownstreamApiRequest
        {
            ApiName = apiName,
            RelativePath = relativePath,
            Method = HttpMethod.Get
        }, cancellationToken);
    }

    /// <summary>Sends GET, requires successful HTTP status, reads web JSON and disposes the response.</summary>
    /// <typeparam name="T">JSON result type.</typeparam>
    /// <returns>The deserialized result, which may be null.</returns>
    /// <exception cref="HttpRequestException">The response status is unsuccessful.</exception>
    public static async Task<T?> GetFromJsonAsync<T>(
        this IDownstreamApiInvoker invoker,
        string apiName,
        string relativePath = "",
        CancellationToken cancellationToken = default)
    {
        using var response = await invoker.GetAsync(apiName, relativePath, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
