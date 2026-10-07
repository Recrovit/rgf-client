namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Host-defined CustomFunction names for an AI turn.</summary>
public sealed class RgfAiCustomFunctionOptions
{
    public required string FunctionName { get; init; }

    public required string RequestParameterName { get; init; }

    public required string ResponseDataName { get; init; }
}
