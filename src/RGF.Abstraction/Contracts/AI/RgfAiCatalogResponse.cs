#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>Public data from the configured AI catalog.</summary>
public sealed record RgfAiCatalogResponse(string DefaultProvider, IReadOnlyList<RgfAiProviderCatalogItem> Providers);

/// <summary>A provider's available models and default model.</summary>
public sealed record RgfAiProviderCatalogItem(string Id, string DefaultModel, IReadOnlyList<RgfAiModelCatalogItem> Models);

/// <summary>A model's supported reasoning settings and defaults.</summary>
public sealed record RgfAiModelCatalogItem(string Id, IReadOnlyList<string> Modes, IReadOnlyList<string> Efforts,
    string? DefaultMode, string? DefaultEffort);
