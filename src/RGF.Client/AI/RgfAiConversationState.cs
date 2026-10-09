using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Client-side identity, model selection and presentation history for an AI conversation.</summary>
public sealed class RgfAiConversationState
{
    private readonly List<RgfAiMessage> messages = [];

    public RgfAiConversationState()
    {
        Messages = messages.AsReadOnly();
    }

    public string? ConversationId { get; private set; }

    public string? ConversationToken { get; private set; }

    public string? AiModelOverride { get; set; }

    public string? AiReasoningEffortOverride { get; set; }

    /// <summary>Indicates whether the conversation is waiting to resume an existing workflow run.</summary>
    public bool IsPendingWorkflow { get; private set; }

    /// <summary>Presentation history; authoritative conversation history belongs to the backend.</summary>
    public IReadOnlyList<RgfAiMessage> Messages { get; }

    /// <summary>Adds a displayed user message without creating or sending a request.</summary>
    public void AddUserMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        messages.Add(new RgfAiMessage { Role = RgfAiMessageRole.User, Message = message });
    }

    /// <summary>Creates a turn containing only the current instruction, without changing presentation history.</summary>
    public RgfAiRequest CreateRequest(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new RgfAiRequest
        {
            CurrentUserMessage = message,
            ConversationId = ConversationId,
            ConversationToken = ConversationToken,
            AiModelOverride = AiModelOverride,
            AiReasoningEffortOverride = AiReasoningEffortOverride
        };
    }

    /// <summary>Updates identity and appends the response only when the turn succeeded.</summary>
    public void ApplyResponse(RgfAiResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Success)
        {
            return;
        }

        ConversationId = response.ConversationId;
        ConversationToken = response.ConversationToken;
        IsPendingWorkflow = response.WorkflowStatus == "WaitingForInput";
        messages.Add(response);
    }

    /// <summary>Clears conversation identity and presentation history, preserving model selection.</summary>
    public void Reset()
    {
        ConversationId = null;
        ConversationToken = null;
        IsPendingWorkflow = false;
        messages.Clear();
    }
}
