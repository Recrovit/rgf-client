using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using System.Text;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.recroby.integration
/// <summary>Sends general Recroby turns through the authenticated RGF API client.</summary>
public sealed class RgfAiApiTransport : IRgfAiTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRgfApiService api;

    public RgfAiApiTransport(IRgfApiService api)
    {
        ArgumentNullException.ThrowIfNull(api);
        this.api = api;
    }

    public async Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
        var response = await api.PostAsync<RgfAiResponse>("/api/rgf/ai/recroby", content,
            cancellationToken: cancellationToken);
        // ApiService reports cancellation as a failed result; preserve the transport cancellation contract.
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.Success)
            throw new HttpRequestException("The Recroby API request failed.", null, response.StatusCode);
        return response.Result ?? throw new InvalidOperationException("The Recroby API returned no AI response.");
    }
}
