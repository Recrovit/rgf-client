#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

/// <summary>Stable machine-readable reasons for denying server-side AI Credit access.</summary>
public enum AiCreditDenialReason
{
    /// <summary>The user is disabled for AI Credit access.</summary>
    UserDisabled = 1,

    /// <summary>The remaining Credit does not meet the required positive balance or configured minimum.</summary>
    InsufficientCredit = 2,

    /// <summary>The prepaid balance has expired.</summary>
    BalanceExpired = 3,

    /// <summary>Credit configuration is missing or invalid, including an unknown mode or missing periods.</summary>
    InvalidConfiguration = 4,

    /// <summary>The user does not exist.</summary>
    UserNotFound = 5,

    /// <summary>An infrastructure failure prevented a reliable access decision.</summary>
    InfrastructureFailure = 6
}
