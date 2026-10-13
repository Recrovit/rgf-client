#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-credit-access
// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>The immutable result of a finalized AI Credit settlement, not an access decision.</summary>
/// <param name="RequestId">The identifier of the settled provider call.</param>
/// <param name="ChargedCredit">The actual finalized Credit charge rounded to eight decimal places; repeated requests return the original charge.</param>
/// <param name="IsNewSettlement">True for a new settlement; false when the request was already settled.</param>
public sealed record AiCreditUsageResult(string RequestId, decimal ChargedCredit, bool IsNewSettlement);
