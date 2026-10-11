#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;

// RGF-DOC: rgf.client.abstraction.ai-credit-access
/// <summary>Provider-independent LLM usage data for Credit accounting.</summary>
/// <remarks>
/// ProviderId is a configured alias; ModelId is the actual provider-returned model.
/// Null token values are unknown; TotalTokens is independently reported, and cached/reasoning tokens may be subsets.
/// </remarks>
public sealed record AiCreditUsage(
    string RequestId,
    string? ProviderId,
    string? ModelId,
    string? ExecutionRoute,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    long? CachedInputTokens,
    long? ReasoningTokens);
