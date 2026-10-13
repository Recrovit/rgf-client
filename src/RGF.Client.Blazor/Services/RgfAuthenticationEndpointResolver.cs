using Microsoft.Extensions.Configuration;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

// RGF-DOC: rgf.client.blazor-sessionauth.configuration
// RGF-DOC: rgf.client.blazor.wasm-authentication
// RGF-DOC: rgf.client.blazor-host-openidconnect.server-side-proxy

/// <summary>Resolves authentication paths from Authentication:Host:EndpointBasePath; blank or invalid prefixes fall back to /authentication.</summary>
public sealed class RgfAuthenticationEndpointResolver(IConfiguration configuration)
{
    private const string DefaultEndpointBasePath = "/authentication";

    /// <summary>Normalized authentication prefix without a trailing slash.</summary>
    public string BasePath { get; } = NormalizeBasePath(configuration.GetValue<string>("Authentication:Host:EndpointBasePath"));

    /// <summary>Login path used for reauthentication navigation.</summary>
    public string LoginPath => $"{BasePath}/login";

    /// <summary>Host logout endpoint path.</summary>
    public string LogoutPath => $"{BasePath}/logout";

    /// <summary>Host session validation endpoint path.</summary>
    public string SessionPath => $"{BasePath}/session";

    /// <summary>Host principal snapshot endpoint path.</summary>
    public string PrincipalPath => $"{BasePath}/principal";

    private static string NormalizeBasePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return DefaultEndpointBasePath;
        }

        var normalizedPath = configuredPath.Trim();
        if (!normalizedPath.StartsWith("/", StringComparison.Ordinal) || normalizedPath.StartsWith("//", StringComparison.Ordinal))
        {
            return DefaultEndpointBasePath;
        }

        return normalizedPath.TrimEnd('/');
    }
}
