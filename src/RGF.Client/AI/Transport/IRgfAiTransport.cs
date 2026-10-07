using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Sends one AI turn and returns its final response.</summary>
public interface IRgfAiTransport
{
    Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default);
}
