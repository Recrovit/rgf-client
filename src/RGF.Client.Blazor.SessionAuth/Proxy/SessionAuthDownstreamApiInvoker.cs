using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;

internal sealed class SessionAuthDownstreamApiInvoker(IHttpClientFactory httpClientFactory) : IDownstreamApiInvoker
{
    public async Task<HttpResponseMessage> SendAsync(DownstreamApiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApiName))
        {
            throw new ArgumentException("The downstream API name is required.", nameof(request));
        }

        using var message = new HttpRequestMessage(request.Method, BuildProxyUri(request))
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

        var client = httpClientFactory.CreateClient(RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName);
        return await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static string BuildProxyUri(DownstreamApiRequest request)
    {
        var apiName = Uri.EscapeDataString(request.ApiName.Trim());
        var relativePath = request.RelativePath.TrimStart('/');

        return string.IsNullOrEmpty(relativePath)
            ? $"/downstream/{apiName}"
            : $"/downstream/{apiName}/{relativePath}";
    }
}
