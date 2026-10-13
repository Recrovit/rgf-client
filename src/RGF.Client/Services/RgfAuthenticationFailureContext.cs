using System.Net;

namespace Recrovit.RecroGridFramework.Client.Services;

/// <summary>HTTP failure metadata passed to the current authentication adapter.</summary>
public sealed class RgfAuthenticationFailureContext
{
    /// <summary>Header name used by the host to distinguish required reauthentication.</summary>
    public const string ReauthenticationRequiredHeaderName = "X-Recrovit-Auth";

    /// <summary>Header value signalling required reauthentication.</summary>
    public const string ReauthenticationRequiredHeaderValue = "reauth-required";

    /// <summary>Status code of the failed response.</summary>
    public required HttpStatusCode StatusCode { get; init; }

    /// <summary>Failed request URI when available.</summary>
    public string? RequestUri { get; init; }

    /// <summary>Whether the response contains the explicit host reauthentication signal.</summary>
    public bool IsReauthenticationRequired { get; init; }

    /// <summary>Response headers available to the authentication adapter.</summary>
    public IReadOnlyDictionary<string, string[]> ResponseHeaders { get; init; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
}
