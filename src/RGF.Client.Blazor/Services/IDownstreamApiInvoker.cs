using System.Net.Http.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

/// <summary>
/// Sends requests to named downstream APIs without exposing the current Blazor runtime mode.
/// </summary>
public interface IDownstreamApiInvoker
{
    Task<HttpResponseMessage> SendAsync(DownstreamApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a request to a configured downstream API.
/// </summary>
public sealed class DownstreamApiRequest
{
    public required string ApiName { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public HttpMethod Method { get; init; } = HttpMethod.Get;

    public HttpContent? Content { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Headers { get; init; }
}

/// <summary>
/// Convenience helpers for downstream API requests.
/// </summary>
public static class DownstreamApiInvokerExtensions
{
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
