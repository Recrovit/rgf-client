#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

/// <summary>The resulting message and execution metadata for an AI turn.</summary>
public class RgfAiResponse : RgfAiMessage
{
    public RgfAiResponse()
    {
        Role = RgfAiMessageRole.Assistant;
    }

    public bool Success { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Reported usage, or null when the host does not supply usage information.</summary>
    public RgfAiUsage? Usage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowRunId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowStatus { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationToken { get; set; }
}
