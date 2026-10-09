using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using System.Runtime.CompilerServices;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration
public partial class RgfRecrobyComponent : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<IServiceProvider, RgfRecrobyCatalogCache> Catalogs = new();
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Parameter] public EventCallback<Exception> SendFailed { get; set; }
    [Parameter] public RgfAiConversationSession? Session { get; set; }

    private RgfAiConversationSession? _session;
    private RgfAiConversationSession? _subscribed;
    private RgfAiCatalogResponse? _catalog;
    private readonly string _selectorId = $"recroby-selection-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _lifetime = new();
    private ElementReference _element;
    private IJSObjectReference? _module;
    private bool _sessionChanged;
    private bool _disposed;
    private bool _catalogLoading;
    private RgfAiConversationSession ActiveSession => Session ?? _session!;
    private bool SelectionDisabled => ActiveSession.IsProcessing || ActiveSession.Conversation.IsPendingWorkflow;
    private string SelectedProvider { get => Provider?.Id ?? string.Empty; set => SelectProvider(value); }
    private string SelectedModel { get => Model?.Id ?? string.Empty; set => SelectModel(value); }
    private string SelectedEffort { get => ActiveSession.Conversation.AiReasoningEffortOverride ?? string.Empty; set => SelectEffort(value); }

    private RgfAiProviderCatalogItem? Provider
    {
        get
        {
            var alias = ActiveSession.Conversation.AiModelOverride?.Split('/');
            var id = alias?.Length == 2 ? alias[0] : _catalog?.DefaultProvider;
            return _catalog?.Providers.FirstOrDefault(provider => provider.Id == id);
        }
    }
    private RgfAiModelCatalogItem? Model => Provider?.Models.FirstOrDefault(model => model.Id ==
        (ActiveSession.Conversation.AiModelOverride?.Split('/')[^1] ?? Provider.DefaultModel));
    private IEnumerable<KeyValuePair<string, string>> ProviderItems =>
        _catalog?.Providers.Select(provider => new KeyValuePair<string, string>(provider.Id, provider.Id)) ?? [];
    private IEnumerable<KeyValuePair<string, string>> ModelItems =>
        Provider?.Models.Select(model => new KeyValuePair<string, string>(model.Id, model.Id)) ?? [];
    private IEnumerable<KeyValuePair<string, string>> EffortItems =>
        new[] { new KeyValuePair<string, string>(string.Empty, "Default") }.Concat(
            Model?.Efforts.Select(effort => new KeyValuePair<string, string>(effort, effort)) ?? []);

    protected override void OnParametersSet()
    {
        if (Session is null)
            _session ??= new(new RgfAiApiTransport(Services.GetRequiredService<IRgfApiService>()));
        if (ReferenceEquals(_subscribed, ActiveSession)) return;
        if (_subscribed is not null) _subscribed.StateChanged -= SessionChanged;
        _subscribed = ActiveSession;
        _subscribed.StateChanged += SessionChanged;
        _sessionChanged = true;
    }

    protected override Task OnParametersSetAsync() => LoadCatalogAsync();

    private async Task LoadCatalogAsync()
    {
        if (_disposed || _catalog is not null || _catalogLoading) return;
        _catalogLoading = true;
        try
        {
            var cache = Catalogs.GetValue(Services, services => new(services.GetRequiredService<IRgfApiService>()));
            var catalog = await cache.GetAsync(_lifetime.Token);
            if (!_disposed) _catalog = catalog;
        }
        catch (Exception) when (!_lifetime.IsCancellationRequested)
        {
            // The catalog is optional; chat remains usable with the existing defaults.
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        finally { _catalogLoading = false; }
    }

    private void SessionChanged()
    {
        if (!_disposed) _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            StateHasChanged();
            if (!ActiveSession.IsProcessing && _catalog is null)
            {
                await LoadCatalogAsync();
                if (!_disposed) StateHasChanged();
            }
        });
    }

    private void SelectProvider(string id)
    {
        if (SelectionDisabled) return;
        var provider = _catalog?.Providers.FirstOrDefault(item => item.Id == id);
        if (provider is null || !provider.Models.Any(model => model.Id == provider.DefaultModel)) return;
        SetModel(provider.Id, provider.DefaultModel);
    }

    private void SelectModel(string id)
    {
        if (SelectionDisabled || Provider is not { } provider || !provider.Models.Any(model => model.Id == id)) return;
        SetModel(provider.Id, id);
    }

    private void SetModel(string provider, string model)
    {
        var alias = $"{provider}/{model}";
        if (ActiveSession.Conversation.AiModelOverride == alias) return;
        ActiveSession.Conversation.AiModelOverride = alias;
        ActiveSession.Conversation.AiReasoningEffortOverride = null;
    }

    private void SelectEffort(string effort)
    {
        if (SelectionDisabled || (effort.Length > 0 && Model?.Efforts.Contains(effort) != true)) return;
        // An explicit alias also identifies the default route when the user makes a selection.
        if (ActiveSession.Conversation.AiModelOverride is null && Provider is { } provider && Model is { } model)
            ActiveSession.Conversation.AiModelOverride = $"{provider.Id}/{model.Id}";
        ActiveSession.Conversation.AiReasoningEffortOverride = effort.Length == 0 ? null : effort;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _catalog?.Providers.Count is not > 0) return;
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import",
            $"{RgfClientConfiguration.AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/RgfRecrobyComponent.razor.js");
        if (_module is null || _disposed) return;
        await _module.InvokeVoidAsync("sync", _element, SelectionDisabled || _sessionChanged);
        _sessionChanged = false;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_subscribed is not null) _subscribed.StateChanged -= SessionChanged;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        _lifetime.Dispose();
    }
}
