using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

/// <summary>Request settings for a typed RGF HTTP operation, independent of its response payload.</summary>
public interface IRgfApiRequest
{
    /// <summary>API-relative request path used by the configured client.</summary>
    string Uri { get; }

    /// <summary>Encoded query string applied to GET requests.</summary>
    string Query { get; }

    /// <summary>Selects the authenticated named client when true.</summary>
    bool AuthClient { get; }

    /// <summary>Optional body used by POST requests.</summary>
    HttpContent Content { get; }

    /// <summary>Optional HTTP headers added alongside package-version headers.</summary>
    Dictionary<string, string> AdditionalHeaders { get; }

    /// <summary>Cancellation passed to HTTP sending and response reading.</summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>Mutable request descriptor; constructor query values are URL-encoded and empty values are omitted.</summary>
public class ApiRequest : IRgfApiRequest
{
    /// <summary>Creates request settings without sending an HTTP call.</summary>
    /// <param name="uri">API-relative path.</param>
    /// <param name="content">Optional POST body.</param>
    /// <param name="query">Optional GET parameters; keys and nonempty values are URL-encoded.</param>
    /// <param name="additionalHeaders">Optional headers retained by reference.</param>
    /// <param name="cancellationToken">Cancellation for the eventual call.</param>
    /// <param name="authClient">Whether to select the authenticated client; defaults to true.</param>
    public ApiRequest(string uri, HttpContent content = null, Dictionary<string, string> query = null, Dictionary<string, string> additionalHeaders = null,
        CancellationToken cancellationToken = default, bool authClient = true)
    {
        Uri = uri;
        Content = content;
        AdditionalHeaders = additionalHeaders;
        CancellationToken = cancellationToken;
        AuthClient = authClient;
        if (query?.Any() == true)
        {
            var encodedParams = query
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .Select(kv => $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}");

            this.Query = string.Join("&", encodedParams);
        }
    }

    /// <inheritdoc />
    public string Uri { get; set; }

    /// <inheritdoc />
    public string Query { get; set; }

    /// <inheritdoc />
    public bool AuthClient { get; set; }

    /// <inheritdoc />
    public HttpContent Content { get; set; }

    /// <inheritdoc />
    public Dictionary<string, string> AdditionalHeaders { get; set; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; set; }
}