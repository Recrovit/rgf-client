#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>A continuation choice with its stable input code and localized display text.</summary>
public sealed class RgfAiChoice
{
    /// <summary>The code to submit unchanged in CurrentUserMessage.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The display text localized by the server for the authenticated user.</summary>
    public string UserText { get; set; } = string.Empty;
}
