using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

public interface IRgfApiRequest
{
    string Uri { get; }

    string Query { get; }

    Version Version { get; }

    bool AuthClient { get; }

    HttpContent Content { get; }

    Dictionary<string, string> HeaderParam { get; }

    CancellationToken CancellationToken { get; }
}

public class ApiRequest : IRgfApiRequest
{
    public ApiRequest(string uri, HttpContent content = null, string query = null, Dictionary<string, string> headerParam = null, CancellationToken cancellationToken = default, Version version = null, bool authClient = true)
    {
        Uri = uri;
        Query = query;
        Content = content;
        HeaderParam = headerParam;
        CancellationToken = cancellationToken;
        Version = version;
        AuthClient = authClient;
    }

    public string Uri { get; set; }

    public string Query { get; set; }

    public Version Version { get; set; }

    public bool AuthClient { get; set; }

    public HttpContent Content { get; set; }

    public Dictionary<string, string> HeaderParam { get; set; }

    public CancellationToken CancellationToken { get; set; }
}
