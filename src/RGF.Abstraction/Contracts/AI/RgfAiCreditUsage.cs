#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>Credit usage for processing one Recroby request, independent of ISO currency costs.</summary>
/// <remarks>Servers aggregate each settlement once and disclose token data only to the data administrator.</remarks>
public class RgfAiCreditUsage
{
    /// <summary>Elapsed request execution time, or null when unavailable.</summary>
    public TimeSpan? ExecutionTime { get; set; }

    /// <summary>Number of distinct logical LLM calls actually started in this request, including calls without settled usage.</summary>
    public int LlmCallCount { get; set; }

    /// <summary>Reasoning effort explicitly supplied when sending this request; null means no explicit effort was supplied.</summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>The sum of distinct finalized settlements for this request; null means unknown and zero means no charge.</summary>
    public decimal? ChargedCredit { get; set; }

    /// <summary>Usage grouped by accounting provider/model alias pair; a request may use multiple pairs.</summary>
    public IReadOnlyList<RgfAiModelCreditUsage> Models { get; set; } = [];

    /// <summary>Optional request token data; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiTokenUsage? Tokens { get; set; }
}
