#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-contracts
// RGF-DOC: rgf.client.abstraction.ai-credit-access
/// <summary>Defines server-side AI Credit access, state queries and usage accounting independently of the data model and AI runtime.</summary>
public interface IAiCreditService
{
    /// <summary>Gets the user's Credit mode.</summary>
    /// <param name="userId">RGF Identity identifier; caller authorization belongs to the host.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The current Credit mode.</returns>
    Task<AiCreditMode> GetCreditModeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets prepaid state, or null in another mode, without modifying stored state.</summary>
    /// <param name="userId">RGF Identity identifier; caller authorization belongs to the host.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>A prepaid snapshot, or null outside prepaid mode.</returns>
    Task<AiPrepaidBalance?> GetPrepaidBalanceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets effective UTC period snapshots without persisting rollovers; returns an empty list in other modes or when periods are missing.</summary>
    /// <param name="userId">RGF Identity identifier; caller authorization belongs to the host.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>Independent effective period snapshots; empty when no periods apply.</returns>
    Task<IReadOnlyList<AiPeriodCredit>> GetPeriodCreditsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the specified user may initiate an LLM call.</summary>
    /// <param name="userId">RGF Identity identifier; caller authorization belongs to the host.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decision and stable denial reason; no Credit reservation is made.</returns>
    Task<AiCreditAccessResult> CheckAccessAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Accounts for the user's completed LLM usage according to provider, model and usage data.</summary>
    /// <remarks>
    /// Implementations must be atomic, concurrency-safe and idempotent by RequestId.
    /// Accounting is not an access check; consumed Credit must be accounted for even beyond current limits.
    /// </remarks>
    /// <param name="userId">RGF Identity identifier whose usage is being settled.</param>
    /// <param name="usage">One provider call with a nonblank request identifier and nonnegative reported counts; null counts mean unknown.</param>
    /// <param name="cancellationToken">Cancels this accounting operation.</param>
    /// <returns>Settlement completion; no cost or access decision is returned.</returns>
    /// <remarks>Do not synthesize totals or replace unknown usage with zero. Missing pricing must not silently settle as free usage.
    /// Disabled and unlimited modes do not modify balances. Failures and cancellation propagate.</remarks>
    Task ApplyUsageAsync(string userId, AiCreditUsage usage, CancellationToken cancellationToken = default);
}
