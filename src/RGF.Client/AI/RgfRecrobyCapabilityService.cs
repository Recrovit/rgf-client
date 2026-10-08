using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.recroby.integration
/// <summary>Queries effective backend feature enablement and preserves the last known state on transient failures.</summary>
public sealed class RgfRecrobyCapabilityService(IRgfApiService api)
{
    private bool? enabled;

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
