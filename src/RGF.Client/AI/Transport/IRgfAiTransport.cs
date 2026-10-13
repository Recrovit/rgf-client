using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Sends one AI turn and returns its final response.</summary>
public interface IRgfAiTransport
{
    /// <summary>Sends one turn without owning presentation history or authoritative backend history.</summary>
    /// <param name="request">Current instruction and optional continuation metadata.</param>
    /// <param name="cancellationToken">Cancels the adapter operation; server cancellation guarantees are transport-specific.</param>
    /// <returns>The final AI response, which may represent an unsuccessful workflow.</returns>
    /// <remarks>Transport/protocol failures propagate as exceptions rather than synthetic AI responses.</remarks>
    Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default);
}
