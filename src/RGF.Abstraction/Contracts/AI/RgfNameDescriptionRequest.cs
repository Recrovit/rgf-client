#nullable enable

using Recrovit.RecroGridFramework.Abstraction.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.recroby.text-generation
// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>Fields requested from the object-independent generation endpoint.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RgfNameDescriptionTarget>))]
public enum RgfNameDescriptionTarget { Name, Description, Both }

/// <summary>Untrusted object input; the server authorizes and prepares all generation context.</summary>
public sealed record RgfNameDescriptionRequest
{
    [Required, MaxLength(100)]
    public required string ObjectType { get; init; }
    public RgfNameDescriptionTarget Target { get; init; }
    public required JsonElement ObjectData { get; init; }
    [MaxLength(200)]
    public string? ExistingName { get; init; }
    [MaxLength(4000)]
    public string? ExistingDescription { get; init; }
    public string? AiProviderOverride { get; init; }
    public string? AiModelOverride { get; init; }
}

/// <summary>Atomic generation result; unrequested fields are null.</summary>
public sealed record RgfNameDescriptionResponse(string? Name, string? Description);

/// <summary>Current detached Filter editor snapshot. Session and Grid identify an already initialized owned Grid.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RgfFilterNameDescriptionData
{
    public required string SessionId { get; init; }
    public required string GridId { get; init; }
    public required RgfFilter.Condition[] Conditions { get; init; }
}
