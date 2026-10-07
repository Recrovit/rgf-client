using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Owns the turn lifecycle for one UI-independent conversation.</summary>
public sealed class RgfAiConversationSession
{
    private readonly IRgfAiTransport transport;
    private int processing;

    public RgfAiConversationSession(IRgfAiTransport transport, RgfAiConversationState? conversation = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        this.transport = transport;
        Conversation = conversation ?? new();
    }

    public RgfAiConversationState Conversation { get; }

    public bool IsProcessing => Volatile.Read(ref processing) != 0;

    public event Action? StateChanged;

    public async Task<RgfAiResponse> SendAsync(string instruction, bool addToPresentationHistory = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        if (Interlocked.CompareExchange(ref processing, 1, 0) != 0)
        {
            throw new InvalidOperationException("An AI turn is already running in this conversation session.");
        }

        try
        {
            if (addToPresentationHistory)
            {
                Conversation.AddUserMessage(instruction);
            }
            StateChanged?.Invoke();
            var request = Conversation.CreateRequest(instruction);
            var response = await transport.SendAsync(request, cancellationToken);
            Conversation.ApplyResponse(response);
            StateChanged?.Invoke();
            return response;
        }
        finally
        {
            Volatile.Write(ref processing, 0);
            StateChanged?.Invoke();
        }
    }
}
