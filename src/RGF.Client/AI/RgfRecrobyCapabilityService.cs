using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.recroby.integration
/// <summary>Queries effective backend feature enablement and preserves the last known state on transient failures.</summary>
public sealed class RgfRecrobyCapabilityService(IRgfApiService api)
{
    private bool? enabled;

    /// <summary>Gets backend enablement, retaining successful state for this service instance.</summary>
    /// <param name="cancellationToken">Cancellation for the capability request.</param>
    /// <returns>Successful backend state, or true when no successful state is available.</returns>
    /// <remarks>Failed lookups may retry on later calls; no polling or cross-scope cache is provided.</remarks>
    public async Task<bool> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        if (enabled.HasValue) return enabled.Value;
        try
        {
            var response = await api.GetAsync<RgfCapabilitiesResponse>("/api/rgf/capabilities",
                cancellationToken: cancellationToken, authClient: false);
            if (response.Success && response.Result is not null) enabled = response.Result.RecrobyEnabled;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        return enabled ?? true;
    }
}
