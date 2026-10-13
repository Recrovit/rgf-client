using System.Net.Http.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

/// <summary>
/// Sends requests to the current host application without exposing the current Blazor runtime mode.
/// </summary>
public interface IHostApiInvoker
{
    /// <summary>Sends a request through the registered host-session transport.</summary>
    /// <param name="request">Endpoint, method, optional content and headers.</param>
    /// <param name="cancellationToken">Cancels HTTP sending.</param>
    /// <returns>The response with headers read; the caller owns disposal and status handling.</returns>
    Task<HttpResponseMessage> SendAsync(HostApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a request to a same-origin host API endpoint.
/// </summary>
public sealed class HostApiRequest
{
    /// <summary>Nonblank host path; the SessionAuth adapter trims it and adds a leading slash.</summary>
    public required string Path { get; init; }

    /// <summary>HTTP method; defaults to GET.</summary>
    public HttpMethod Method { get; init; } = HttpMethod.Get;

    /// <summary>Optional body transferred to the outgoing request; do not reuse it after sending.</summary>
    public HttpContent? Content { get; init; }

    /// <summary>Optional request headers, with content-header fallback when a body is present.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Headers { get; init; }
}

/// <summary>
/// Convenience helpers for host API requests.
/// </summary>
public static class HostApiInvokerExtensions
{
    /// <summary>Sends GET through this invoker; the caller owns response disposal and status handling.</summary>
    public static Task<HttpResponseMessage> GetAsync(
        this IHostApiInvoker invoker,
        string path,
        CancellationToken cancellationToken = default)
    {
        return invoker.SendAsync(new HostApiRequest
        {
            Path = path,
            Method = HttpMethod.Get
        }, cancellationToken);
    }

    /// <summary>Sends GET, requires successful HTTP status, reads web JSON and disposes the response.</summary>
    /// <typeparam name="T">JSON result type.</typeparam>
    /// <returns>The deserialized result, which may be null.</returns>
    /// <exception cref="HttpRequestException">The response status is unsuccessful.</exception>
    public static async Task<T?> GetFromJsonAsync<T>(
        this IHostApiInvoker invoker,
        string path,
        CancellationToken cancellationToken = default)
    {
        using var response = await invoker.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
