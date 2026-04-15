using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

public interface IRgfApiRequest
{
    string Uri { get; }

    string Query { get; }

    bool AuthClient { get; }

    HttpContent Content { get; }

    Dictionary<string, string> AdditionalHeaders { get; }

    CancellationToken CancellationToken { get; }
}

public class ApiRequest : IRgfApiRequest
{
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

    public string Uri { get; set; }

    public string Query { get; set; }

    public bool AuthClient { get; set; }

    public HttpContent Content { get; set; }

    public Dictionary<string, string> AdditionalHeaders { get; set; }

    public CancellationToken CancellationToken { get; set; }
}