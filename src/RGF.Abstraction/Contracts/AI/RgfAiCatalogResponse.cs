#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>Public data from the configured AI catalog.</summary>
/// <param name="DefaultProvider">Configured default provider alias.</param>
/// <param name="Providers">Public provider aliases and capabilities; an empty list is supported.</param>
public sealed record RgfAiCatalogResponse(string DefaultProvider, IReadOnlyList<RgfAiProviderCatalogItem> Providers);

/// <summary>A provider's available models and default model.</summary>
/// <param name="Id">Configured provider alias, without credentials.</param>
/// <param name="DefaultModel">Default model alias for this provider.</param>
/// <param name="Models">Available model aliases.</param>
public sealed record RgfAiProviderCatalogItem(string Id, string DefaultModel, IReadOnlyList<RgfAiModelCatalogItem> Models);

/// <summary>A model's supported reasoning settings and defaults.</summary>
/// <param name="Id">Configured model alias, not a native provider configuration object.</param>
/// <param name="Modes">Supported reasoning mode names.</param>
/// <param name="Efforts">Supported reasoning effort tokens.</param>
/// <param name="DefaultMode">Configured mode, or null when unspecified.</param>
/// <param name="DefaultEffort">Configured effort, or null when unspecified.</param>
public sealed record RgfAiModelCatalogItem(string Id, IReadOnlyList<string> Modes, IReadOnlyList<string> Efforts,
    string? DefaultMode, string? DefaultEffort);
