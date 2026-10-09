using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration
internal sealed class RgfRecrobyCatalogCache(IRgfApiService api)
{
    private readonly object _gate = new();
    private Task<RgfAiCatalogResponse?>? _request;

    public Task<RgfAiCatalogResponse?> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<RgfAiCatalogResponse?> request;
        lock (_gate)
        {
            // Share in-flight requests and successful catalogs; a failed lookup can be retried.
            if (_request is null || (_request.IsCompletedSuccessfully && _request.Result is null))
                _request = FetchAsync();
            request = _request;
        }
        // Closing one conversation must not cancel a lookup used by another conversation.
        return request.WaitAsync(cancellationToken);
    }

    private async Task<RgfAiCatalogResponse?> FetchAsync()
    {
        try
        {
            var response = await api.GetAsync<RgfAiCatalogResponse>("/api/rgf/ai/recroby/catalog");
            if (response.Success && response.Result is { Providers: not null } catalog
                && catalog.Providers.All(provider => provider is { Models: not null }
                    && provider.Models.All(model => model is { Efforts: not null })))
                return catalog;
        }
        catch (Exception)
        {
            // The catalog is optional; keep default chat available after transport or protocol failures.
        }
        return null;
    }
}
