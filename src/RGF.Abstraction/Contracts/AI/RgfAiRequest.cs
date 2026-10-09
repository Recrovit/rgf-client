#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>A conversation turn with one or more messages and optional execution settings.</summary>
public class RgfAiRequest
{
    /// <summary>Messages for this turn in chronological order. Complete authoritative history is not required; backend runtimes may own it.</summary>
    public List<RgfAiMessage> Messages { get; set; } = [];

    /// <summary>Convenience access to the current user message in <see cref="Messages"/>.</summary>
    public string CurrentUserMessage
    {
        get => Messages.LastOrDefault(message => message.Role == RgfAiMessageRole.User)?.Message ?? string.Empty;
        set
        {
            var message = Messages.LastOrDefault(message => message.Role == RgfAiMessageRole.User);
            if (message is null)
                Messages.Add(new RgfAiMessage { Role = RgfAiMessageRole.User, Message = value });
            else
                message.Message = value;
        }
    }

    /// <summary>The conversation to continue, or null to start a new conversation.</summary>
    public virtual string? ConversationId { get; set; }

    /// <summary>An opaque host-issued token for authorized conversation continuation.</summary>
    public string? ConversationToken { get; set; }

    /// <summary>An optional model identifier interpreted by the host.</summary>
    public string? AiModelOverride { get; set; }

    /// <summary>An optional reasoning effort; null preserves the configured default.</summary>
    public string? AiReasoningEffortOverride { get; set; }

    /// <summary>Host-defined extension values; keys have no meaning in the shared protocol.</summary>
    public Dictionary<string, object?>? CustomParams { get; set; }
}
