#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>Reported token counts; null means unknown and zero means a reported zero.</summary>
/// <remarks>Do not replace unknown values with zero or derive counts from other counts. The server owns disclosure authorization.</remarks>
public class RgfAiTokenUsage
{
    /// <summary>Reported input tokens, or null when unknown.</summary>
    public long? InputTokens { get; set; }

    /// <summary>Reported output tokens, or null when unknown.</summary>
    public long? OutputTokens { get; set; }

    /// <summary>Independently reported total tokens, or null when unknown.</summary>
    public long? TotalTokens { get; set; }

    /// <summary>Reported cached input tokens, which may be a subset of input tokens, or null when unknown.</summary>
    public long? CachedInputTokens { get; set; }

    /// <summary>Reported reasoning tokens, which may be a subset of output tokens, or null when unknown.</summary>
    public long? ReasoningTokens { get; set; }
}
