#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-contracts
// RGF-DOC: rgf.client.abstraction.ai-credit-access
/// <summary>Checks server-side AI Credit access independently of the user data model and AI runtime.</summary>
/// <remarks>
/// Hosts can supply their own implementation through dependency injection.
/// Implementations must deny Disabled mode and allow Unlimited mode for an existing user.
/// Prepaid mode requires an unexpired balance with sufficient remaining Credit.
/// Periodic mode requires at least one configured period and sufficient remaining Credit in every configured period.
/// The minimum Credit is configured and validated by the implementation, without a prescribed configuration source.
/// A zero minimum still requires a positive balance; a positive minimum requires a balance at least equal to it.
/// Missing users, unknown modes and invalid or missing Credit configuration must never grant access.
/// Infrastructure failures must deny access or propagate an exception, never grant access automatically.
/// This check neither reserves Credit nor estimates the cost of the next LLM call.
/// Actual usage accounting is outside this interface and may be introduced through a separate contract.
/// State queries never persist changes. Empty identifiers cause ArgumentException, missing users
/// cause KeyNotFoundException, and invalid mode or period configuration causes InvalidOperationException.
/// Query infrastructure failures and cancellation propagate; separate queries are independent snapshots.
/// </remarks>
public interface IAiCreditService
{
    /// <summary>Gets the user's Credit mode. Throws for a missing user or invalid configuration.</summary>
    Task<AiCreditMode> GetCreditModeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets prepaid state, or null in another mode. Does not modify stored state.</summary>
    Task<AiPrepaidBalance?> GetPrepaidBalanceAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets effective UTC period snapshots without persisting rollovers. Returns an empty list in other modes or when periods are missing.</summary>
    Task<IReadOnlyList<AiPeriodCredit>> GetPeriodCreditsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the specified user may initiate an LLM call.</summary>
    /// <param name="userId">The user identifier interpreted by the server-side implementation.</param>
    /// <param name="cancellationToken">A token used to cancel the check.</param>
    /// <returns>An access decision with a stable reason when access is denied.</returns>
    Task<AiCreditAccessResult> CheckAccessAsync(string userId, CancellationToken cancellationToken = default);
}
