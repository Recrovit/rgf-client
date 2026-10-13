namespace Recrovit.RecroGridFramework.Client.Handlers;

/// <summary>
/// Provides the incoming request cookie header for SSR proxy calls.
/// </summary>
public interface IRgfServerRequestCookieAccessor
{
    /// <summary>Gets the incoming SSR Cookie header, or null when no request cookie is available.</summary>
    string? GetCookieHeader();
}

/// <summary>Default SSR accessor that supplies no cookies until replaced by a host integration.</summary>
public sealed class NoOpRgfServerRequestCookieAccessor : IRgfServerRequestCookieAccessor
{
    /// <inheritdoc />
    public string? GetCookieHeader() => null;
}