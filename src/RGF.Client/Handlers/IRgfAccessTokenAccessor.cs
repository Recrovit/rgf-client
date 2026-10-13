namespace Recrovit.RecroGridFramework.Client.Handlers;

/// <summary>Authentication adapter for obtaining the current user access token when available.</summary>
public interface IRgfAccessTokenAccessor
{
    /// <summary>Gets the current token, or null when the adapter provides no bearer token.</summary>
    Task<string?> GetAccessTokenAsync();
}

/// <summary>Default accessor for integrations that do not expose a browser bearer token.</summary>
public sealed class NoOpRgfAccessTokenAccessor : IRgfAccessTokenAccessor
{
    /// <inheritdoc />
    public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
}
