using System.Net.Http.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

/// <summary>
/// Sends requests to the current host application without exposing the current Blazor runtime mode.
/// </summary>
public interface IHostApiInvoker
{
    Task<HttpResponseMessage> SendAsync(HostApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a request to a same-origin host API endpoint.
/// </summary>
public sealed class HostApiRequest
{
    public required string Path { get; init; }

    public HttpMethod Method { get; init; } = HttpMethod.Get;

    public HttpContent? Content { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Headers { get; init; }
}

/// <summary>
/// Convenience helpers for host API requests.
/// </summary>
public static class HostApiInvokerExtensions
{
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
