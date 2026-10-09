#nullable enable
namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-credit-access
/// <summary>The mutually exclusive AI Credit modes.</summary>
public enum AiCreditMode
{
    /// <summary>AI access is disabled.</summary>
    Disabled = 0,
    /// <summary>AI access has no Credit limit.</summary>
    Unlimited = 1,
    /// <summary>AI access is subject to periodic limits.</summary>
    Periodic = 2,
    /// <summary>AI access requires a prepaid balance.</summary>
    Prepaid = 3
}

/// <summary>The supported UTC calendar periods.</summary>
public enum AiCreditPeriodType
{
    /// <summary>A calendar day.</summary>
    Daily = 1,
    /// <summary>A calendar week starting on Monday.</summary>
    Weekly = 2,
    /// <summary>A calendar month.</summary>
    Monthly = 3
}

/// <summary>A prepaid balance evaluated at a UTC instant. A null balance indicates missing configuration.</summary>
public sealed record AiPrepaidBalance(decimal? Balance, DateOnly? ExpiresOn, bool IsExpired, DateTime AsOfUtc);

/// <summary>A periodic Credit snapshot after applying any due rollover in memory.</summary>
public sealed record AiPeriodCredit(AiCreditPeriodType PeriodType, decimal CreditLimit, decimal UsedCredit,
    DateTime ResetsAtUtc, DateTime AsOfUtc)
{
    /// <summary>The remaining Credit, which can be negative after overspending.</summary>
    public decimal RemainingCredit => CreditLimit - UsedCredit;
}
