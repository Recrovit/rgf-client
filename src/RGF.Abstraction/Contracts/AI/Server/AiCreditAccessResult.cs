#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>An immutable server-side AI Credit access decision.</summary>
/// <param name="DenialReason">The stable denial reason, or null when access is allowed.</param>
public sealed record AiCreditAccessResult(AiCreditDenialReason? DenialReason)
{
    /// <summary>Whether the user may initiate an LLM call.</summary>
    public bool IsAllowed => DenialReason is null;
}
