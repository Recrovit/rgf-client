#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>Aggregated Credit usage for one provider/model combination within a Recroby request.</summary>
public class RgfAiModelCreditUsage
{
    /// <summary>The configured provider identifier, or null when unknown.</summary>
    public string? ProviderId { get; set; }

    /// <summary>The actual model identifier, or null when unknown; do not infer it from a configured alias.</summary>
    public string? ModelId { get; set; }

    /// <summary>The finalized Credit charge for this combination, independent of ISO currency costs.</summary>
    public decimal ChargedCredit { get; set; }

    /// <summary>Optional model token data; omitted from JSON when null and disclosed only to the data administrator.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfAiTokenUsage? Tokens { get; set; }
}
