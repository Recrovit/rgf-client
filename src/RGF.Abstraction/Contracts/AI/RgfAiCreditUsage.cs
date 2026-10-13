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

    /// <summary>The sum of distinct finalized settlements for this request; null means unknown and zero means no charge.</summary>
    public decimal? ChargedCredit { get; set; }

    /// <summary>Usage grouped by provider/model combination; a request may use multiple combinations.</summary>
    public IReadOnlyList<RgfAiModelCreditUsage> Models { get; set; } = [];

    /// <summary>Optional request token data; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiTokenUsage? Tokens { get; set; }
}
