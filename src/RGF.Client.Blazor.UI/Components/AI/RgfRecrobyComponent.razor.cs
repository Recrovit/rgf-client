using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using System.Runtime.CompilerServices;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.blazor-ui.ai-chat
// RGF-DOC: rgf.client.recroby.integration

/// <summary>Recroby chat with optional catalog selectors and an external or lazily created API conversation session.</summary>
public partial class RgfRecrobyComponent : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<IServiceProvider, RgfRecrobyCatalogCache> Catalogs = [];

    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    [Inject]
    private IRecroSecService RecroSec { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = null!;

    /// <summary>Optional callback forwarded unchanged for manual chat send exceptions.</summary>
    [Parameter]
    public EventCallback<Exception> SendFailed { get; set; }

    /// <summary>External session to retain; when null the component creates its own API-backed session.</summary>
    [Parameter]
    public RgfAiConversationSession? Session { get; set; }

    private RgfAiConversationSession? _session;
    private RgfAiConversationSession? _subscribed;
    private RgfAiCatalogResponse? _catalog;
    private readonly string _selectorId = $"recroby-selection-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _lifetime = new();
    private ElementReference _element;
    private bool _jsInitialized;
    private bool _sessionChanged;
    private const string JsNamespace = RGFClientBlazorUIConfiguration.JsBlazorUiNamespace + ".AI.Selector";
    private bool _disposed;
    private RgfAiChatComponent? _chat;
    private readonly object _disposalSync = new();
    private Task? _disposal;
    private bool _catalogLoading;
    private string? _lastRenderedLanguage;
    private RgfAiConversationSession ActiveSession => Session ?? _session!;
    private bool SelectionDisabled => ActiveSession.IsProcessing || ActiveSession.Conversation.IsPendingWorkflow;
    private string SelectedProvider { get => Provider?.Id ?? string.Empty; set => SelectProvider(value); }
    private string SelectedModel { get => Model?.Id ?? string.Empty; set => SelectModel(value); }
    private string SelectedEffort { get => ActiveSession.Conversation.AiReasoningEffortOverride ?? string.Empty; set => SelectEffort(value); }
    private string DisplayEffort => ActiveSession.Conversation.AiReasoningEffortOverride ?? Model?.DefaultEffort ?? RecroDict.GetRgfUiString("Default");

    private string SelectedFormat
    {
        get => (ActiveSession.Conversation.ResponseFormat ?? RgfAiResponseFormat.PlainText).ToString();
        set
        {
            if (!SelectionDisabled && Enum.TryParse<RgfAiResponseFormat>(value, out var format) && Enum.IsDefined(format))
                ActiveSession.Conversation.ResponseFormat = format;
        }
    }
    private string DisplayFormat => FormatLabel(SelectedFormat);
    private IEnumerable<KeyValuePair<string, string>> FormatItems => Enum.GetNames<RgfAiResponseFormat>()
        .Select(format => new KeyValuePair<string, string>(format, FormatLabel(format)));

    private string FormatLabel(string format) => format switch
    {
        nameof(RgfAiResponseFormat.PlainText) => RecroDict.GetRgfRecrobyString("PlainText"),
        nameof(RgfAiResponseFormat.Minimal) => RecroDict.GetRgfRecrobyString("FormatMinimal"),
        nameof(RgfAiResponseFormat.Normal) => RecroDict.GetRgfRecrobyString("FormatNormal"),
        nameof(RgfAiResponseFormat.Maximum) => RecroDict.GetRgfRecrobyString("FormatMaximum"),
        _ => format
    };

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
        new[] { new KeyValuePair<string, string>(string.Empty, RecroDict.GetRgfUiString("Default")) }.Concat(
            Model?.Efforts.Select(effort => new KeyValuePair<string, string>(effort, effort)) ?? []);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RecroSec.LanguageChangedEvent.Subscribe(OnLanguageChangedAsync);
    }

    private Task OnLanguageChangedAsync(DataEventArgs<string> _) => InvokeAsync(() =>
    {
        if (!_disposed) StateHasChanged();
    });

    protected override Task OnParametersSetAsync()
    {
        if (Session is null)
            _session ??= new(new RgfAiApiTransport(Services.GetRequiredService<IRgfApiService>()));
        // A language switch rerenders the Workspace and reapplies its unchanged session.
        // A subsequent ordinary parameter update must still retry a failed catalog lookup.
        var languageChanged = _lastRenderedLanguage is not null
            && !string.Equals(_lastRenderedLanguage, RecroSec.UserLanguage, StringComparison.OrdinalIgnoreCase);
        if (ReferenceEquals(_subscribed, ActiveSession))
            return languageChanged ? Task.CompletedTask : LoadCatalogAsync();
        var conversation = ActiveSession.Conversation;
        if (conversation.ResponseFormat is null && conversation.ConversationId is null && conversation.ConversationToken is null
            && conversation.Messages.Count == 0 && !ActiveSession.IsProcessing && !conversation.IsPendingWorkflow)
            conversation.ResponseFormat = RgfAiResponseFormat.Minimal;
        if (_subscribed is not null) _subscribed.StateChanged -= SessionChanged;
        _subscribed = ActiveSession;
        _subscribed.StateChanged += SessionChanged;
        _sessionChanged = true;
        return LoadCatalogAsync();
    }

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
        try
        {
            if (_disposed) return;
            _lastRenderedLanguage = RecroSec.UserLanguage;
            await RGFClientBlazorUIConfiguration.WaitForAiScriptsAsync(_jsRuntime);
            if (_disposed) return;

            _jsInitialized = true;
            await _jsRuntime.InvokeVoidAsync(JsNamespace + ".sync", _element, SelectionDisabled || _sessionChanged);
            _sessionChanged = false;
        }
        catch (JSDisconnectedException) { }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_disposalSync)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
        }
        return new(CompleteDisposalAsync(completion));
    }

    private async Task CompleteDisposalAsync(TaskCompletionSource completion)
    {
        try
        {
            await DisposeCoreAsync();
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
        await completion.Task;
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        RecroSec.LanguageChangedEvent.Unsubscribe(OnLanguageChangedAsync);
        _chat?.Dispose();
        _lifetime.Cancel();
        if (_subscribed is not null) _subscribed.StateChanged -= SessionChanged;
        try
        {
            if (_jsInitialized) await _jsRuntime.InvokeVoidAsync(JsNamespace + ".dispose", _element);
        }
        catch (JSDisconnectedException) { }
        finally { _lifetime.Dispose(); }
    }
}
