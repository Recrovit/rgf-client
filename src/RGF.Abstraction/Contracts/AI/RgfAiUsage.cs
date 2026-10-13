#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>Token counts, monetary costs and elapsed time for an AI request.</summary>
public class RgfAiUsage
{
    /// <summary>Reported input token count; this DTO neither prices nor validates usage.</summary>
    public long InputTokens { get; set; }

    /// <summary>Reported output token count; use a null response Usage when no usage data is available.</summary>
    public long OutputTokens { get; set; }

    /// <summary>Input cost in major units of <see cref="Currency"/>, not cents.</summary>
    public decimal InputCost { get; set; }

    /// <summary>Output cost in major units of <see cref="Currency"/>, not cents.</summary>
    public decimal OutputCost { get; set; }

    /// <summary>ISO 4217 currency code, for example EUR or USD; null when monetary costs are unavailable.</summary>
    public string? Currency { get; set; }

    /// <summary>Elapsed execution duration.</summary>
    public TimeSpan ExecutionTime { get; set; }
}
