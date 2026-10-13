#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts

/// <summary>The author of a conversation message.</summary>
public enum RgfAiMessageRole
{
    /// <summary>A message authored by the user.</summary>
    User = 0,
    /// <summary>A message authored by the assistant.</summary>
    Assistant = 1
}
