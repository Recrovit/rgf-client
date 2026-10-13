using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Owns the turn lifecycle for one UI-independent conversation.</summary>
public sealed class RgfAiConversationSession
{
    private readonly IRgfAiTransport transport;
    private int processing;

    /// <summary>Creates an independent turn coordinator over the supplied transport.</summary>
    /// <param name="transport">Backend invocation adapter; must not be null.</param>
    /// <param name="conversation">Existing state to retain, or null for new empty state.</param>
    public RgfAiConversationSession(IRgfAiTransport transport, RgfAiConversationState? conversation = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        this.transport = transport;
        Conversation = conversation ?? new();
    }

    /// <summary>Identity, selections and presentation history owned by this session.</summary>
    public RgfAiConversationState Conversation { get; }

    /// <summary>Whether this session currently owns an in-flight turn.</summary>
    public bool IsProcessing => Volatile.Read(ref processing) != 0;

    /// <summary>Synchronous notification at processing start, response application and completion; handlers should be short and marshal UI work.</summary>
    public event Action? StateChanged;

    /// <summary>Sends one turn and applies a successful response to the conversation state.</summary>
    /// <param name="instruction">Nonblank current instruction; it is forwarded without trimming.</param>
    /// <param name="addToPresentationHistory">Whether to append the user instruction before sending; defaults to true.</param>
    /// <param name="cancellationToken">Cancellation delegated to the transport.</param>
    /// <returns>The original response, including unsuccessful workflow responses.</returns>
    /// <remarks>Only one turn may run per session. Transport and notification exceptions propagate;
    /// processing is cleared in finally. A displayed user message remains after failure or cancellation.
    /// Do not reset or mutate conversation state while sending.</remarks>
    /// <exception cref="ArgumentException">The instruction is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The instruction is null.</exception>
    /// <exception cref="InvalidOperationException">A turn is already running in this session.</exception>
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
