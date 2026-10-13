#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.recroby.integration

/// <summary>The state of RGF features enabled by the backend.</summary>
/// <param name="RecrobyEnabled">Backend feature enablement; required in JSON so a missing field is not interpreted as disabled.</param>
public sealed record RgfCapabilitiesResponse([property: System.Text.Json.Serialization.JsonRequired] bool RecrobyEnabled);

/// <summary>Stable error codes for the shared AI protocol.</summary>
public static class RgfAiErrorCodes
{
    /// <summary>A request could not run because the backend has no configured AI provider.</summary>
    public const string AiProviderNotConfigured = "AiProviderNotConfigured";
}
