using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

// RGF-DOC: rgf.client.recroby.integration

/// <summary>Desktop placement preference; mobile presentation uses the visible viewport independently.</summary>
public enum RgfRecrobyWindowMode
{
    /// <summary>Movable viewport panel without reserved host space.</summary>
    Floating,
    /// <summary>Panel fixed to the left edge with reserved work-area overlap.</summary>
    DockLeft,
    /// <summary>Panel fixed to the right edge with reserved work-area overlap.</summary>
    DockRight,
    /// <summary>Panel fixed to the bottom with an independently scrolling desktop work area.</summary>
    DockBottom
}

/// <summary>Browser UI preferences only; contains no conversation data.</summary>
public sealed class RgfRecrobyLayoutSettings
{
    /// <summary>Requested desktop window mode, normalized by the browser module.</summary>
    public RgfRecrobyWindowMode Mode { get; set; }
    /// <summary>Whether only the edge tab is visible; defaults to true.</summary>
    public bool IsCollapsed { get; set; } = true;
    /// <summary>Floating horizontal position in CSS pixels; defaults to 48.</summary>
    public double X { get; set; } = 48;
    /// <summary>Floating vertical position in CSS pixels; defaults to 80.</summary>
    public double Y { get; set; } = 80;
    /// <summary>Floating width in CSS pixels; defaults to 440 before viewport normalization.</summary>
    public double Width { get; set; } = 440;
    /// <summary>Floating height in CSS pixels; defaults to 560 before viewport normalization.</summary>
    public double Height { get; set; } = 560;
    /// <summary>Side-dock width in CSS pixels; defaults to 400 before bounds are applied.</summary>
    public double DockWidth { get; set; } = 400;
    /// <summary>Bottom-dock height in CSS pixels; defaults to 320 before bounds are applied.</summary>
    public double DockHeight { get; set; } = 320;
}

/// <summary>Owns in-memory conversation sessions and browser layout preferences around the application work area.</summary>
public partial class RgfRecrobyWorkspace : IAsyncDisposable
{
    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>Application content that shares available space with the docked desktop panel.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private readonly List<Conversation> _conversations = [];
    private Conversation? _active;
    private Conversation? _editing;
    private string _editTitle = string.Empty;
    private bool _focusTitleEditor;
    private bool HasMultipleTypes => _conversations.Select(c => c.RecrobyType).Distinct().Skip(1).Any();
    private static string TypeLabel(string type) => type == "general" ? "General chat" : type;
    private string ConversationLabel(Conversation conversation) => HasMultipleTypes
        ? $"{TypeLabel(conversation.RecrobyType)}: {conversation.Title}" : conversation.Title;

    private void BeginRename()
    {
        if (_active == null) return;
        CloseSwitcher(false);
        _editing = _active;
        _editTitle = _active.Title;
        _focusTitleEditor = true;
    }

    private void CancelRename()
    {
        _editing = null;
        _focusTitleEditor = false;
    }

    private void RenameKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape") CancelRename();
        else if (args.Key == "Enter" && _editing != null && !string.IsNullOrWhiteSpace(_editTitle))
        {
            var title = _editTitle.Trim();
            if (StringInfo.ParseCombiningCharacters(title).Length > 100) return;
            _editing.Title = title;
            _editing.NeedsAutomaticTitle = false;
            CancelRename();
        }
    }

    private void ConversationStateChanged(Conversation conversation)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed || !_conversations.Contains(conversation) || !conversation.NeedsAutomaticTitle) return;
            var prompt = conversation.Session.Conversation.Messages.FirstOrDefault(m => m.Role == RgfAiMessageRole.User)?.Message;
            if (prompt == null) return;
            var title = Regex.Replace(prompt, @"\s+", " ").Trim();
            var characters = StringInfo.ParseCombiningCharacters(title);
            conversation.Title = characters.Length > 30 ? title[..characters[29]] + "…" : title;
            conversation.NeedsAutomaticTitle = false;
            StateHasChanged();
        });
    }
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
        CancelRename();
        _layout.IsCollapsed = true;
    }

    private void SelectConversation(Conversation conversation)
    {
        CancelRename();
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
        if (settings.IsCollapsed) { CloseSwitcher(false); CancelRename(); }
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
            if (_focusTitleEditor && _editing != null)
            {
                _focusTitleEditor = false;
                await _module.InvokeVoidAsync("focusTitleEditor", _element);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        foreach (var conversation in _conversations) conversation.Session.StateChanged -= conversation.StateChangedHandler;
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
        CancelRename();
        CloseSwitcher(_switcherOpen);
        // Resolve the API only when sending a turn, so an unconfigured backend cannot break startup.
        _active = new Conversation(new RgfAiConversationSession(new LazyTransport(Services)));
        var conversation = _active;
        conversation.StateChangedHandler = () => ConversationStateChanged(conversation);
        conversation.Session.StateChanged += conversation.StateChangedHandler;
        _conversations.Add(_active);
    }

    private void CloseConversation(Conversation conversation)
    {
        conversation.Session.StateChanged -= conversation.StateChangedHandler;
        if (_editing == conversation) CancelRename();
        _conversations.Remove(conversation);
        if (_active == conversation) _active = _conversations.LastOrDefault();
        if (_conversations.Count == 0) NewConversation();
    }

    // The type identifies the general Recroby independently of its displayed title.
    private sealed class Conversation(RgfAiConversationSession session)
    {
        public string Title { get; set; } = "New chat";
        public bool NeedsAutomaticTitle { get; set; } = true;
        public RgfAiConversationSession Session { get; } = session;
        public Action? StateChangedHandler { get; set; }
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
