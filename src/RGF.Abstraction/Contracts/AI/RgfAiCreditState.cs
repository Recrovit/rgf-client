#nullable enable

using Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-credit-access
// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>The current user's Credit state using the existing server snapshot contracts.</summary>
public class RgfAiCreditState
{
    /// <summary>Disabled has no usable balance; Unlimited has no numeric limit but may have usage; Periodic and Prepaid use their snapshots.</summary>
    public AiCreditMode Mode { get; set; }

    /// <summary>The current prepaid balance, expiration date and expiration status, or null when unavailable or not applicable.</summary>
    public AiPrepaidBalance? Prepaid { get; set; }

    /// <summary>Independent snapshots for all simultaneous periodic limits; empty when none apply.</summary>
    public IReadOnlyList<AiPeriodCredit> Periods { get; set; } = [];
}
