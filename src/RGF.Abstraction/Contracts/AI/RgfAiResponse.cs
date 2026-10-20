#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>The resulting message and execution metadata for an AI turn.</summary>
public class RgfAiResponse : RgfAiMessage
{
    /// <summary>Creates a response with Assistant role; success defaults to false and identity to an empty string.</summary>
    public RgfAiResponse()
    {
        Role = RgfAiMessageRole.Assistant;
    }

    /// <summary>Whether the host considers the turn successful; independent from HTTP transport success.</summary>
    public bool Success { get; set; }

    /// <summary>The machine-readable error code for a failed request.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>Backend conversation identifier; defaults to empty when no conversation was created.</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>Reported usage, or null when the host does not supply usage information.</summary>
    public RgfAiUsage? Usage { get; set; }

    /// <summary>Optional Credit usage for this request, available on successful or failed responses.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiCreditUsage? CreditUsage { get; set; }

    /// <summary>Optional current user Credit state, available on successful or failed responses.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiCreditState? CreditState { get; set; }

    /// <summary>Optional backend workflow run identifier; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowRunId { get; set; }

    /// <summary>Optional host-supplied workflow status; no shared enum or status validation is imposed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowStatus { get; set; }

    /// <summary>Optional input mode for a waiting continuation; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiResponseMode? ResponseMode { get; set; }

    /// <summary>Optional localized choices for a waiting continuation. Submit the unchanged code as CurrentUserMessage.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RgfAiChoice>? Choices { get; set; }

    /// <summary>Optional opaque token for the next authorized turn; the host owns protection and validation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationToken { get; set; }
}
