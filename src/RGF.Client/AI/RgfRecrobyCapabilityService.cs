using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.recroby.integration
// RGF-DOC: rgf.client.blazor.filtering
/// <summary>Queries effective backend feature enablement and preserves the last known state on transient failures.</summary>
public sealed class RgfRecrobyCapabilityService(IRgfApiService api)
{
    private volatile RgfCapabilitiesResponse? capabilities;
    private readonly SemaphoreSlim requestLock = new(1, 1);

    /// <summary>Gets backend enablement, retaining successful state for this service instance.</summary>
    /// <param name="cancellationToken">Cancellation for the capability request.</param>
    /// <returns>Successful backend state, or true when no successful state is available.</returns>
    /// <remarks>Failed lookups may retry on later calls; no polling or cross-scope cache is provided.</remarks>
    public async Task<bool> GetEnabledAsync(CancellationToken cancellationToken = default)
        => (await GetCapabilitiesAsync(cancellationToken))?.RecrobyEnabled ?? true;

    /// <summary>Gets whether to show the Filter editor checkbox, defaulting to true when unavailable.</summary>
    /// <param name="cancellationToken">Cancellation for the capability request.</param>
    public async Task<bool> GetAllowFilterToLowerAsync(CancellationToken cancellationToken = default)
        => (await GetCapabilitiesAsync(cancellationToken))?.AllowFilterToLower ?? true;

    private async Task<RgfCapabilitiesResponse?> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (capabilities is { } cached) return cached;
        await requestLock.WaitAsync(cancellationToken);
        try
        {
            if (capabilities is { } current) return current;
            var response = await api.GetAsync<RgfCapabilitiesResponse>("/api/rgf/capabilities",
                cancellationToken: cancellationToken, authClient: false);
            if (response.Success) capabilities = response.Result;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            requestLock.Release();
        }
        return capabilities;
    }
}
