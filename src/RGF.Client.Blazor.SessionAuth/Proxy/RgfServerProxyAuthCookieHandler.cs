using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;

// RGF-DOC: rgf.client.blazor-sessionauth.configuration

/// <summary>
/// Forwards the incoming authentication cookie when SSR code calls the local host proxy.
/// </summary>
/// <remarks>
/// Cookie forwarding applies only to requests initially targeting the configured host origin.
/// Requests to that origin without Origin or Sec-Fetch-Site receive the configured Host Origin as a request-intent signal.
/// Existing browser signals are preserved; the signal is not a user credential.
/// Redirect handling is controlled by the HTTP client, not by this handler.
/// The Host-to-downstream API authentication boundary is handled separately.
/// </remarks>
public sealed class RgfServerProxyAuthCookieHandler(IRgfServerRequestCookieAccessor cookieAccessor) : DelegatingHandler
{
    private const string CookieHeaderName = "Cookie";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsLocalProxyRequest(request.RequestUri)
            && !request.Headers.Contains("Origin")
            && !request.Headers.Contains("Sec-Fetch-Site"))
        {
            request.Headers.TryAddWithoutValidation("Origin", new Uri(ApiService.BaseAddress).GetLeftPart(UriPartial.Authority));
        }

        if (ShouldForwardCookie(request, out var cookieHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(CookieHeaderName, cookieHeaderValue);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private bool ShouldForwardCookie(HttpRequestMessage request, out string cookieHeaderValue)
    {
        cookieHeaderValue = string.Empty;

        if (request.Headers.Contains(CookieHeaderName))
        {
            return false;
        }

        if (!IsLocalProxyRequest(request.RequestUri))
        {
            return false;
        }

        var cookie = cookieAccessor.GetCookieHeader();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return false;
        }

        cookieHeaderValue = cookie;
        return true;
    }

    private static bool IsLocalProxyRequest(Uri? requestUri)
    {
        if (requestUri is not { IsAbsoluteUri: true })
        {
            return false;
        }

        if (!Uri.TryCreate(ApiService.BaseAddress, UriKind.Absolute, out var proxyBaseAddress)
            || (proxyBaseAddress.Scheme != Uri.UriSchemeHttp && proxyBaseAddress.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(proxyBaseAddress.UserInfo))
        {
            return false;
        }

        return Uri.Compare(requestUri, proxyBaseAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
