using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;

internal sealed class SessionAuthHostApiInvoker(IHttpClientFactory httpClientFactory) : IHostApiInvoker
{
    public async Task<HttpResponseMessage> SendAsync(HostApiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("The host API path is required.", nameof(request));
        }

        using var message = new HttpRequestMessage(request.Method, BuildRequestUri(request))
        {
            Content = request.Content
        };

        if (request.Headers is not null)
        {
            foreach (var header in request.Headers)
            {
                var values = header.Value.ToArray();
                if (!message.Headers.TryAddWithoutValidation(header.Key, values))
                {
                    message.Content?.Headers.TryAddWithoutValidation(header.Key, values);
                }
            }
        }

        var client = httpClientFactory.CreateClient(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName);
        return await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static string BuildRequestUri(HostApiRequest request)
    {
        var path = request.Path.Trim();

        return path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : $"/{path}";
    }
}
