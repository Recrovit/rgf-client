#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-contracts
// RGF-DOC: rgf.client.abstraction.ai-credit-access
/// <summary>Defines server-side AI Credit access, state queries and usage accounting independently of the data model and AI runtime.</summary>
public interface IAiCreditService
{
    /// <summary>Gets the user's Credit mode.</summary>
    Task<AiCreditMode> GetCreditModeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets prepaid state, or null in another mode, without modifying stored state.</summary>
    Task<AiPrepaidBalance?> GetPrepaidBalanceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets effective UTC period snapshots without persisting rollovers; returns an empty list in other modes or when periods are missing.</summary>
    Task<IReadOnlyList<AiPeriodCredit>> GetPeriodCreditsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the specified user may initiate an LLM call.</summary>
    Task<AiCreditAccessResult> CheckAccessAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Accounts for the user's completed LLM usage according to provider, model and usage data.</summary>
    /// <remarks>
    /// Implementations must be atomic, concurrency-safe and idempotent by RequestId.
    /// Accounting is not an access check; consumed Credit must be accounted for even beyond current limits.
    /// </remarks>
    Task ApplyUsageAsync(string userId, AiCreditUsage usage, CancellationToken cancellationToken = default);
}
