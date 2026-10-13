namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Host-defined CustomFunction names for an AI turn.</summary>
public sealed class RgfAiCustomFunctionOptions
{
    /// <summary>Nonblank CustomFunction name provided by the host.</summary>
    public required string FunctionName { get; init; }

    /// <summary>Nonblank custom-parameter key reserved for the AI request; additional parameters may not use it.</summary>
    public required string RequestParameterName { get; init; }

    /// <summary>Nonblank final progress CustomData key containing the JSON-compatible AI response.</summary>
    public required string ResponseDataName { get; init; }
}
