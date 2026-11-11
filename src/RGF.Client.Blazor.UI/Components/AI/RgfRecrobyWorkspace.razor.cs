using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
    /// <summary>Requested desktop window mode; defaults to DockRight and is normalized by the browser module.</summary>
    public RgfRecrobyWindowMode Mode { get; set; } = RgfRecrobyWindowMode.DockRight;
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
    private readonly List<RgfRecrobyGridContext> _gridContexts = [];
    private RgfRecrobyGridContext? _activeGridContext;
    private long _gridActivationVersion;
    private readonly ConditionalWeakTable<IRgManager, GridTurns> _gridTurns = new();
    private Conversation? _active;
    internal (string Provider, string Model)? NameDescriptionModelSelection
    {
        get
        {
            if (_disposed || _layout.IsCollapsed || _active?.Session.Conversation.AiModelOverride is not { } alias) return null;
            var parts = alias.Split('/');
            // Preserve malformed explicit choices so the server rejects them instead of using a default.
            return parts.Length == 2 ? (parts[0], parts[1]) : ("", alias);
        }
    }
    internal RgfRecrobyGridContext? ActiveGridContext => _disposed ? null : _activeGridContext;
    internal event Action? ActiveGridContextChanged;
    private IReadOnlyDictionary<string, string>? _recrobyUi;
    private IRecroSecService? _recroSec;
    private int _dictionaryLoadVersion;
    private string GridMessage(string key, string fallback) =>
        _recrobyUi?.TryGetValue(key, out var value) == true && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    private Conversation? _editing;
    private string _editTitle = string.Empty;
    private bool _focusTitleEditor;
    private bool _focusUserMessage;
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
    private void GridContextChanged()
    {
        if (_disposed) return;
        // A manager can become available again before the renderer processes its invalidation.
        var unavailable = _activeGridContext is { Manager: null } active ? active : null;
        _ = InvokeAsync(() => UpdateActiveGrid(unavailable));
    }

    private void GridContextClosed(RgfRecrobyGridContext context)
    {
        if (_disposed) return;
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            _gridContexts.RemoveAll(candidate => ReferenceEquals(candidate, context));
            DetachGridContext(context);
            UpdateActiveGrid();
        });
    }

    private void UpdateActiveGrid(RgfRecrobyGridContext? unavailable = null)
    {
        if (_disposed) return;
        var active = _gridContexts.FindLast(context => !ReferenceEquals(context, unavailable) &&
            !context.IsClosed && context.Manager is not null);
        if (!ReferenceEquals(_activeGridContext, active))
        {
            _activeGridContext = active;
            ++_gridActivationVersion;
            ActiveGridContextChanged?.Invoke();
        }
        StateHasChanged();
    }

    private void DetachGridContext(RgfRecrobyGridContext context)
    {
        context.Changed -= GridContextChanged;
        context.Closed -= GridContextClosed;
    }

    private RgfRecrobyLayoutSettings _layout = new();
    private readonly string _selectorId = $"recroby-{Guid.NewGuid():N}";
    private ElementReference _element;
    private IJSObjectReference? _module;
    private Task<IJSObjectReference>? _moduleImport;
    private DotNetObjectReference<RgfRecrobyWorkspace>? _reference;
    private bool _disposed;
    private IDisposable? _workspaceRegistration;
    private readonly object _disposalSync = new();
    private Task? _disposal;

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

    private void Open()
    {
        _layout.IsCollapsed = false;
        _focusUserMessage = true;
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
        if (_disposed) return;
        if (firstRender)
        {
            _moduleImport = JS.InvokeAsync<IJSObjectReference>("import",
                $"{RgfClientConfiguration.AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Components/AI/RgfRecrobyWorkspace.razor.js").AsTask();
            _module = await _moduleImport;
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
            if (_focusUserMessage)
            {
                _focusUserMessage = false;
                if (!_layout.IsCollapsed)
                    await _module.InvokeVoidAsync("focusUserMessage", _element);
            }
        }
    }

    /// <summary>Detaches owners and awaits all conversation component and browser cleanup.</summary>
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
        _recroSec?.LanguageChangedEvent?.Unsubscribe(OnLanguageChangedAsync);
        _workspaceRegistration?.Dispose();
        foreach (var context in _gridContexts) DetachGridContext(context);
        _gridContexts.Clear();
        _activeGridContext = null;
        await InvokeAsync(async () =>
        {
            _active = null;
            var notify = ActiveGridContextChanged;
            ActiveGridContextChanged = null;
            notify?.Invoke();
            foreach (var conversation in _conversations.ToArray())
            {
                conversation.Session.StateChanged -= conversation.StateChangedHandler;
                if (conversation.Component is not null) await conversation.Component.DisposeAsync();
            }
            _conversations.Clear();
            CancelRename();
        });
        try
        {
            if (_moduleImport is not null) _module = await _moduleImport;
            if (_module != null)
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); }
    }

    protected override void OnInitialized()
    {
        NewConversation();
        _workspaceRegistration = Services.GetService<RgfRecrobyWorkspaceService>()?.RegisterWorkspace(
            async operation =>
            {
                var result = false;
                await InvokeAsync(() => result = operation());
                return result;
            }, OpenExternalConversation, CloseOwnerAsync,
            context => TrackGrid(context, activate: true), context => TrackGrid(context, activate: false));
    }

    protected override Task OnInitializedAsync()
    {
        _recroSec = Services.GetService<IRecroSecService>();
        _recroSec?.LanguageChangedEvent?.Subscribe(OnLanguageChangedAsync);
        return LoadRecrobyUiAsync(_recroSec?.UserLanguage);
    }

    private Task OnLanguageChangedAsync(DataEventArgs<string> args) =>
        InvokeAsync(() => LoadRecrobyUiAsync(args.Value));

    private async Task LoadRecrobyUiAsync(string? language)
    {
        if (_disposed || Services.GetService<IRecroDictService>() is not { } recroDict) return;
        var version = ++_dictionaryLoadVersion;
        try
        {
            var dictionary = await recroDict.GetDictionaryAsync("RGF.Recroby.UI", language!, authClient: false);
            if (_disposed || version != _dictionaryLoadVersion) return;
            _recrobyUi = dictionary;
        }
        catch (Exception error)
        {
            if (_disposed || version != _dictionaryLoadVersion) return;
            _recrobyUi = null;
            LogGridError(error, "The Recroby UI dictionary could not be loaded.");
        }
        StateHasChanged();
    }

    private bool TrackGrid(RgfRecrobyGridContext context, bool activate)
    {
        if (_disposed || context.IsClosed) return false;
        var index = _gridContexts.FindIndex(candidate => ReferenceEquals(candidate, context));
        if (index >= 0 && !activate) return true;
        if (context.Manager is null) return false;

        if (index >= 0)
        {
            _gridContexts.RemoveAt(index);
        }
        else
        {
            context.Changed += GridContextChanged;
            context.Closed += GridContextClosed;
        }
        _gridContexts.Add(context);
        if (activate) Open();
        UpdateActiveGrid();
        return true;
    }

    private bool OpenExternalConversation(object owner, string type, string contextKey, string title,
        Func<RgfAiConversationSession> sessionFactory)
    {
        if (_disposed) return false;
        var conversation = _conversations.FirstOrDefault(c => !c.Closing && ReferenceEquals(c.Owner, owner)
            && c.RecrobyType == type && c.ContextKey == contextKey);
        if (conversation is null)
        {
            var session = sessionFactory() ?? throw new InvalidOperationException("The session factory returned null.");
            conversation = new Conversation(session) { Owner = owner, ContextKey = contextKey,
                RecrobyType = type, Title = title, NeedsAutomaticTitle = false };
            _conversations.Add(conversation);
        }
        SelectConversation(conversation);
        _layout.IsCollapsed = false;
        StateHasChanged();
        return true;
    }

    private async Task CloseOwnerAsync(object owner)
    {
        if (_disposed)
        {
            if (_disposal is not null) await _disposal;
            return;
        }
        await InvokeAsync(async () =>
        {
            if (_disposed) return;
            foreach (var conversation in _conversations.Where(c => ReferenceEquals(c.Owner, owner)).ToArray())
                await CloseConversation(conversation);
            if (!_disposed) StateHasChanged();
        });
        if (_disposed && _disposal is not null) await _disposal;
    }

    private void NewConversation()
    {
        var previous = _active;
        CancelRename();
        CloseSwitcher(_switcherOpen);
        // Resolve the API only when sending a turn, so an unconfigured backend cannot break startup.
        _active = new Conversation(new RgfAiConversationSession(new LazyTransport(this)));
        var conversation = _active;
        conversation.StateChangedHandler = () => ConversationStateChanged(conversation);
        conversation.Session.StateChanged += conversation.StateChangedHandler;
        _conversations.Add(_active);
        RemoveEmptyConversation(previous);
    }

    private void RemoveEmptyConversation(Conversation? conversation)
    {
        if (conversation is null || conversation.Closing || conversation.Owner is not null) return;
        var session = conversation.Session;
        var state = session.Conversation;
        if (state.Messages.Count != 0 || session.IsProcessing || state.IsPendingWorkflow
            || state.ConversationId is not null || state.ConversationToken is not null) return;

        // Removing the keyed component from the render tree lets Blazor await its async disposal.
        RemoveConversation(conversation);
    }

    private async Task CloseConversation(Conversation conversation)
    {
        conversation.Closing = true;
        conversation.Session.StateChanged -= conversation.StateChangedHandler;
        if (conversation.Component is not null) await conversation.Component.DisposeAsync();
        if (!_conversations.Contains(conversation)) return;
        RemoveConversation(conversation);
        if (_active == conversation) _active = _conversations.LastOrDefault();
        if (!_disposed && _conversations.Count == 0) NewConversation();
    }

    private void RemoveConversation(Conversation conversation)
    {
        conversation.Session.StateChanged -= conversation.StateChangedHandler;
        if (_editing == conversation) CancelRename();
        _conversations.Remove(conversation);
    }

    // The type identifies the general Recroby independently of its displayed title.
    private sealed class Conversation(RgfAiConversationSession session)
    {
        public string Title { get; set; } = "New chat";
        public bool NeedsAutomaticTitle { get; set; } = true;
        public RgfAiConversationSession Session { get; } = session;
        public Action? StateChangedHandler { get; set; }
        public Guid Id { get; } = Guid.NewGuid();
        public string RecrobyType { get; init; } = "general";
        public bool Closing { get; set; }
        public RgfRecrobyComponent? Component { get; set; }
        public object? Owner { get; init; }
        public string? ContextKey { get; init; }
    }

    private sealed class GridTurns
    {
        public long Latest;
    }

    private sealed class LazyTransport(RgfRecrobyWorkspace workspace) : IRgfAiTransport
    {
        private (string? ConversationId, string? Token, GridTarget Target)? _pending;
        private readonly record struct GridTarget(RgfRecrobyGridContext? Context, string? SessionId, string? GridId, int EntityId);

        public async Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RgfRecrobyGridContext? gridContext = null;
            IRgManager? manager = null;
            GridTarget target = default;
            (string? SessionId, string? GridId) managerIds = default;
            GridTurns? turns = null;
            long turn = 0;
            long activationVersion = 0;
            RgfAiResponse? rejection = null;
            await workspace.InvokeAsync(() =>
            {
                gridContext = workspace.ActiveGridContext;
                manager = gridContext?.Manager;
                activationVersion = workspace._gridActivationVersion;
                var session = manager?.SessionParams;
                managerIds = (session?.SessionId, session?.GridId);
                var entityId = manager?.EntityDesc.EntityId ?? 0;
                request.GridRequest = gridContext?.CreateGridRequest();
                target = new(gridContext, request.GridRequest?.SessionId, request.GridRequest?.GridId, entityId);
                if (_pending is { } pending && pending.ConversationId == request.ConversationId &&
                    pending.Token == request.ConversationToken && pending.Target != target)
                {
                    rejection = new RgfAiResponse
                    {
                        ErrorCode = "PendingGridChanged",
                        Message = pending.Target.Context is null
                            ? workspace.GridMessage("RecrobyPendingGridAdded",
                                "This workflow started without a Grid. Close the active Grids to continue, or start a new conversation.")
                            : workspace.GridMessage("RecrobyPendingGridChanged",
                                "Return to the original Grid to continue this workflow, or start a new conversation.")
                    };
                    return;
                }
                if (manager is null) return;
                turns = workspace._gridTurns.GetValue(manager, static _ => new());
                turn = ++turns.Latest;
            });

            if (rejection is not null) return rejection;

            var response = await new RgfAiApiTransport(workspace.Services.GetRequiredService<IRgfApiService>())
                .SendAsync(request, cancellationToken);
            if (response.Success)
                _pending = response.WorkflowStatus == "WaitingForInput"
                    ? (response.ConversationId, response.ConversationToken, target)
                    : null;
            if (response is not { Success: true, FilterAction: { } action }) return response;

            // The conversation keeps response objects as history. Consume the action in this turn.
            response.FilterAction = null;
            await workspace.InvokeAsync(async () =>
            {
                if (workspace._disposed || cancellationToken.IsCancellationRequested || IsSuperseded()) return;

                IRgListHandler? list = null;
                try
                {
                    if (IsTargetCurrent())
                    {
                        var handler = await manager!.GetFilterHandlerAsync();
                        if (workspace._disposed || cancellationToken.IsCancellationRequested || IsSuperseded()) return;
                        if (IsTargetCurrent() && handler is not null &&
                            handler.InitFilter(JsonSerializer.Serialize(action.Conditions)))
                        {
                            list = manager.ListHandler;
                            // Keep editor changes separate from the applied conditions until the user accepts them.
                            var applied = RgfGridSettings.DeepCopy(new RgfGridSettings { Conditions = handler.StoreFilter() })!;
                            list.ApplyFilterState(applied.Conditions!, list.SQLTimeout);
                            handler.SetNewAiFilterSettings(action.Remark);
                        }
                    }
                }
                catch (Exception error)
                {
                    list = null;
                    workspace.LogGridError(error, "The Recroby filter could not be loaded.");
                }

                if (list is null)
                {
                    if (!workspace._disposed && !cancellationToken.IsCancellationRequested && !IsSuperseded())
                        await workspace.ReportFilterLoadFailureAsync(manager);
                }
                else
                    _ = workspace.RefreshAfterFilterLoadAsync(list);
            });
            cancellationToken.ThrowIfCancellationRequested();
            return response;

            bool IsSuperseded() => turns is not null && turns.Latest != turn;

            bool IsTargetCurrent() => manager is not null && ReferenceEquals(gridContext?.Manager, manager) &&
                ReferenceEquals(workspace.ActiveGridContext, gridContext) && activationVersion == workspace._gridActivationVersion &&
                manager.ListHandler is { Initialized: true } &&
                action is { Operation: RgfAiFilterOperation.Replace, Conditions.Length: > 0 } &&
                !string.IsNullOrEmpty(action.SessionId) && !string.IsNullOrEmpty(action.GridId) && action.EntityId > 0 &&
                action.SessionId == target.SessionId && action.GridId == target.GridId && action.EntityId == target.EntityId &&
                action.SessionId == managerIds.SessionId && action.GridId == managerIds.GridId &&
                action.SessionId == manager.SessionParams.SessionId && action.GridId == manager.SessionParams.GridId &&
                action.EntityId == manager.EntityDesc.EntityId;
        }
    }

    private async Task ReportFilterLoadFailureAsync(IRgManager? manager)
    {
        try
        {
            var toast = manager?.ToastManager ?? Services.GetService<IRgfEventNotificationService>()
                ?.GetNotificationManager(RgfToastEventArgs.NotificationManagerScope);
            if (toast is not null)
                await toast.RaiseEventAsync(new RgfToastEventArgs("Recroby",
                    "The generated filter could not be loaded into the Grid.", RgfToastType.Error), this);
        }
        catch (Exception error)
        {
            LogGridError(error, "The Recroby filter error notification could not be shown.");
        }
    }

    private async Task RefreshAfterFilterLoadAsync(IRgListHandler list)
    {
        try
        {
            await list.RefreshDataAsync(null, invalidateAggregates: false);
        }
        catch (Exception error)
        {
            LogGridError(error, "The Grid refresh raised an exception after the Recroby filter was loaded.");
        }
    }

    private void LogGridError(Exception error, string message) =>
        Services.GetService<ILogger<RgfRecrobyWorkspace>>()?.LogWarning(error, "{Message}", message);
}
