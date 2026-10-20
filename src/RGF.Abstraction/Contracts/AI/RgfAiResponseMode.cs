#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>The accepted user input for a pending continuation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RgfAiResponseMode>))]
public enum RgfAiResponseMode
{
    /// <summary>Accepts free text.</summary>
    FreeText,
    /// <summary>Accepts a configured choice code or free text.</summary>
    ChoiceOrText,
    /// <summary>Accepts only a configured choice code.</summary>
    ChoiceOnly
}
