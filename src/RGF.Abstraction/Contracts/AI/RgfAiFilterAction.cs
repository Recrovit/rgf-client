#nullable enable

using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>Identifies a supported AI Grid filter operation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RgfAiFilterOperation>))]
public enum RgfAiFilterOperation
{
    /// <summary>Replaces all active filter conditions.</summary>
    Replace = 1,

    /// <summary>Replaces the applied filter, including with an empty filter, if the original Grid state still matches.</summary>
    ReplaceIfUnchanged = 2
}

/// <summary>Server-validated filter conditions and their target Grid.</summary>
/// <param name="Operation">The operation to perform.</param>
/// <param name="SessionId">The verified RGF session.</param>
/// <param name="GridId">The verified Grid instance.</param>
/// <param name="EntityId">The verified Grid entity.</param>
/// <param name="Conditions">The complete replacement filter in Grid format.</param>
/// <param name="Remark">A genuine filter description; null when only an unavailable-description message exists.</param>
public sealed record RgfAiFilterAction(RgfAiFilterOperation Operation, string SessionId, string GridId,
    int EntityId, RgfFilter.Condition[] Conditions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Remark = null);
