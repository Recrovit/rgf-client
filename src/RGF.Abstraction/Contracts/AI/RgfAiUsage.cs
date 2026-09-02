#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

/// <summary>Token counts, monetary costs and elapsed time for an AI request.</summary>
public class RgfAiUsage
{
    public long InputTokens { get; set; }

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
