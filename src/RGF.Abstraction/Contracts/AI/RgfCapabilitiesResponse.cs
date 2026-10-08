namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

/// <summary>The state of RGF features enabled by the backend.</summary>
public sealed record RgfCapabilitiesResponse([property: System.Text.Json.Serialization.JsonRequired] bool RecrobyEnabled);

/// <summary>Stable error codes for the shared AI protocol.</summary>
public static class RgfAiErrorCodes
{
    public const string AiProviderNotConfigured = "AiProviderNotConfigured";
}
