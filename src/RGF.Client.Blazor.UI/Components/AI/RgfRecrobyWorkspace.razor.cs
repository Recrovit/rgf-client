using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration
public enum RgfRecrobyWindowMode
{
    Floating,
    DockLeft,
    DockRight,
    DockBottom
}

/// <summary>Browser UI preferences only; contains no conversation data.</summary>
public sealed class RgfRecrobyLayoutSettings
{
    public RgfRecrobyWindowMode Mode { get; set; }
    public bool IsCollapsed { get; set; } = true;
    public double X { get; set; } = 48;
    public double Y { get; set; } = 80;
    public double Width { get; set; } = 440;
    public double Height { get; set; } = 560;
    public double DockWidth { get; set; } = 400;
    public double DockHeight { get; set; } = 320;
}

public partial class RgfRecrobyWorkspace : IAsyncDisposable
{
    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private readonly List<Conversation> _conversations = [];
    private Conversation? _active;
    private int _nextTitle;
    private RgfRecrobyLayoutSettings _layout = new();
    private readonly string _selectorId = $"recroby-{Guid.NewGuid():N}";
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<RgfRecrobyWorkspace>? _reference;
    private bool _disposed;

    private void SetMode(RgfRecrobyWindowMode mode) => _layout.Mode = mode;

    private bool _switcherOpen;
    private bool _restoreSwitcherFocus;
    private string _search = string.Empty;
    private IEnumerable<Conversation> FilteredConversations => _conversations.Where(c =>
        _conversations.Count <= 8 || c.Title.Contains(_search, StringComparison.OrdinalIgnoreCase));

    private void ToggleSwitcher()
    {
        if (_switcherOpen) CloseSwitcher();
        else { _search = string.Empty; _switcherOpen = true; }
    }

    private void CloseSwitcher(bool restoreFocus = true)
    {
        _switcherOpen = false;
        _restoreSwitcherFocus = restoreFocus;
        _search = string.Empty;
    }

    private void SwitcherKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && _switcherOpen) CloseSwitcher();
    }

    private void Collapse()
    {
        CloseSwitcher(false);
        _layout.IsCollapsed = true;
    }

    private void SelectConversation(Conversation conversation)
    {
        _active = conversation;
        CloseSwitcher();
    }

    [JSInvokable]
    public Task DismissSwitcher(bool restoreFocus)
    {
        if (_disposed) return Task.CompletedTask;
        return InvokeAsync(() => { CloseSwitcher(restoreFocus); StateHasChanged(); });
    }

    [JSInvokable]
    public Task LayoutChanged(RgfRecrobyLayoutSettings settings)
    {
        if (_disposed || !Enum.IsDefined(settings.Mode)) return Task.CompletedTask;
        _layout = settings;
        if (settings.IsCollapsed) CloseSwitcher(false);
        return InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import",
                $"{RgfClientConfiguration.AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/RgfRecrobyWorkspace.razor.js");
            if (_module != null && !_disposed)
            {
                _reference = DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync("initialize", _element, _reference, _layout);
            }
        }
        else if (_module != null && !_disposed)
        {
            await _module.InvokeVoidAsync("update", _element, _layout);
        }
        if (_module != null && !_disposed)
        {
            await _module.InvokeVoidAsync("syncSwitcher", _element, _switcherOpen, _restoreSwitcherFocus);
            _restoreSwitcherFocus = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module != null)
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); }
    }

    protected override void OnInitialized() => NewConversation();

    private void NewConversation()
    {
        CloseSwitcher(_switcherOpen);
        // Resolve the API only when sending a turn, so an unconfigured backend cannot break startup.
        _active = new Conversation($"Conversation {++_nextTitle}", new RgfAiConversationSession(new LazyTransport(Services)));
        _conversations.Add(_active);
    }

    private void CloseConversation(Conversation conversation)
    {
        _conversations.Remove(conversation);
        if (_active == conversation) _active = _conversations.LastOrDefault();
        if (_conversations.Count == 0) NewConversation();
    }

    // The type identifies the general Recroby independently of its displayed title.
    private sealed record Conversation(string Title, RgfAiConversationSession Session)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string RecrobyType { get; } = "general";
    }

    private sealed class LazyTransport(IServiceProvider services) : IRgfAiTransport
    {
        public Task<Recrovit.RecroGridFramework.Abstraction.Contracts.AI.RgfAiResponse> SendAsync(
            Recrovit.RecroGridFramework.Abstraction.Contracts.AI.RgfAiRequest request, CancellationToken cancellationToken = default)
            => new RgfAiApiTransport(services.GetRequiredService<IRgfApiService>()).SendAsync(request, cancellationToken);
    }
}
