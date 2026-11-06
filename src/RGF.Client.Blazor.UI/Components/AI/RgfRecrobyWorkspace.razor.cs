using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
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
    private readonly HashSet<RgfRecrobyGridContext> _gridContexts = new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<IRgManager, GridTurns> _gridTurns = new();
    private Conversation? _active;
    internal RgfRecrobyGridContext? ActiveGridContext => _disposed ? null : _active?.GridContext;
    internal event Action? ActiveGridContextChanged;
    private bool _gridReconnectBlocked;
    private Conversation? _editing;
    private string _editTitle = string.Empty;
    private bool _focusTitleEditor;
    private bool HasMultipleTypes => _conversations.Select(c => c.RecrobyType).Distinct().Skip(1).Any();
    private static string TypeLabel(string type) => type == "general" ? "General chat" : type;
    private static string ContextLabel(Conversation conversation) => conversation.GridContext is { } context
        ? context.DisplayName + (context.Manager is null ? " (unavailable)" : string.Empty)
        : TypeLabel(conversation.RecrobyType);
    private string ConversationLabel(Conversation conversation) => HasMultipleTypes
        || conversation.GridContext is not null
        ? $"{ContextLabel(conversation)}: {conversation.Title}" : conversation.Title;

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
        _ = InvokeAsync(() =>
        {
            if (_disposed) return;
            ActiveGridContextChanged?.Invoke();
            StateHasChanged();
        });
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
        NotifyActiveConversationChanged();
    }

    private void NotifyActiveConversationChanged()
    {
        _gridReconnectBlocked = false;
        if (!_disposed) ActiveGridContextChanged?.Invoke();
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
        _workspaceRegistration?.Dispose();
        foreach (var context in _gridContexts) context.Changed -= GridContextChanged;
        _gridContexts.Clear();
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
        NewGeneralConversation();
        _workspaceRegistration = Services.GetService<RgfRecrobyWorkspaceService>()?.RegisterWorkspace(
            async operation =>
            {
                var result = false;
                await InvokeAsync(() => result = operation());
                return result;
            }, OpenExternalConversation, CloseOwnerAsync, OpenGridConversation);
    }

    private bool OpenGridConversation(RgfRecrobyGridContext context)
    {
        if (_disposed) return false;
        if (_active is not { Closing: false } || !ReferenceEquals(_active.GridContext, context))
        {
            if (_active is { Closing: false, Owner: null, GridBinding: { Context: { Manager: null } detached } binding } active)
            {
                if (active.Session.IsProcessing || active.Session.Conversation.IsPendingWorkflow)
                {
                    CloseSwitcher(false);
                    _layout.IsCollapsed = false;
                    _gridReconnectBlocked = true;
                    StateHasChanged();
                    return false;
                }
                if (context.Manager is null) return false;
                binding.Context = context;
                ObserveGridContext(context);
                UnobserveUnusedGridContext(detached);
                NotifyActiveConversationChanged();
            }
            else
            {
                var existing = _conversations.FirstOrDefault(c => !c.Closing && ReferenceEquals(c.GridContext, context));
                if (existing is not null)
                {
                    var previous = _active;
                    SelectConversation(existing);
                    RemoveEmptyConversation(previous);
                }
                else CreateConversation(context);
            }
        }
        _gridReconnectBlocked = false;
        _layout.IsCollapsed = false;
        StateHasChanged();
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
        => CreateConversation(_active?.GridContext);

    private void NewGeneralConversation() => CreateConversation(null);

    private void CreateConversation(RgfRecrobyGridContext? context)
    {
        var previous = _active;
        CancelRename();
        CloseSwitcher(_switcherOpen);
        // Resolve the API only when sending a turn, so an unconfigured backend cannot break startup.
        var binding = new GridContextBinding { Context = context };
        _active = new Conversation(new RgfAiConversationSession(new LazyTransport(this, binding))) { GridBinding = binding };
        var conversation = _active;
        conversation.StateChangedHandler = () => ConversationStateChanged(conversation);
        conversation.Session.StateChanged += conversation.StateChangedHandler;
        _conversations.Add(_active);
        if (context is not null) ObserveGridContext(context);
        RemoveEmptyConversation(previous);
        NotifyActiveConversationChanged();
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
        if (!_disposed && _conversations.Count == 0) NewGeneralConversation();
        else NotifyActiveConversationChanged();
    }

    private void RemoveConversation(Conversation conversation)
    {
        conversation.Session.StateChanged -= conversation.StateChangedHandler;
        if (_editing == conversation) CancelRename();
        _conversations.Remove(conversation);
        if (conversation.GridContext is { } context) UnobserveUnusedGridContext(context);
    }

    private void ObserveGridContext(RgfRecrobyGridContext context)
    {
        if (_gridContexts.Add(context)) context.Changed += GridContextChanged;
    }

    private void UnobserveUnusedGridContext(RgfRecrobyGridContext context)
    {
        if (!_conversations.Any(c => ReferenceEquals(c.GridContext, context)) && _gridContexts.Remove(context))
            context.Changed -= GridContextChanged;
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
        public GridContextBinding? GridBinding { get; init; }
        public RgfRecrobyGridContext? GridContext => GridBinding?.Context;
    }

    private sealed class GridContextBinding
    {
        public RgfRecrobyGridContext? Context { get; set; }
    }

    private sealed class GridTurns
    {
        public long Latest;
    }

    private sealed class LazyTransport(RgfRecrobyWorkspace workspace, GridContextBinding binding) : IRgfAiTransport
    {
        public async Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RgfRecrobyGridContext? gridContext = null;
            IRgManager? manager = null;
            var entityId = 0;
            GridTurns? turns = null;
            long turn = 0;
            await workspace.InvokeAsync(() =>
            {
                gridContext = binding.Context;
                manager = gridContext?.Manager;
                request.GridRequest = gridContext?.CreateGridRequest();
                if (manager is null) return;
                entityId = manager.EntityDesc.EntityId;
                turns = workspace._gridTurns.GetValue(manager, static _ => new());
                turn = ++turns.Latest;
            });

            var response = await new RgfAiApiTransport(workspace.Services.GetRequiredService<IRgfApiService>())
                .SendAsync(request, cancellationToken);
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
                ReferenceEquals(binding.Context, gridContext) &&
                manager.ListHandler is { Initialized: true } && request.GridRequest is { } sent &&
                action is { Operation: RgfAiFilterOperation.Replace, Conditions.Length: > 0 } &&
                !string.IsNullOrEmpty(action.SessionId) && !string.IsNullOrEmpty(action.GridId) && action.EntityId > 0 &&
                action.SessionId == sent.SessionId && action.GridId == sent.GridId && action.EntityId == entityId &&
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
