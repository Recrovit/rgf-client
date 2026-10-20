using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

// RGF-DOC: rgf.client.blazor-ui.entity-ui

public partial class RgfEntityComponent : ComponentBase, IDisposable
{
    public static RenderFragment Create(RgfEntityParameters entityParameters, ILogger? logger = null)
    {
        Type? type;
        if (string.IsNullOrEmpty(entityParameters.EntityName) ||
            !RgfBlazorConfiguration.EntityComponentTypes.TryGetValue(entityParameters.EntityName, out type))
        {
            type = RgfBlazorConfiguration.EntityComponentTypes[string.Empty];
        }
        return builder =>
        {
            logger?.LogDebug("Create | EntityName:{EntityName}, GridId:{GridId}", entityParameters.EntityName, entityParameters.GridId);
            int sequence = 0;
            builder.OpenComponent(sequence++, type);
            builder.AddAttribute(sequence++, nameof(RgfEntityComponent.EntityParameters), entityParameters);
            builder.CloseComponent();
        };
    }

    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = null!;

    [Inject]
    private ILogger<RgfEntityComponent> _logger { get; set; } = null!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = default!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    public IRgManager? Manager { get; set; }

    public List<IDisposable> Disposables { get; private set; } = [];

    private CancellationTokenSource? _createManagerCancellationTokenSource;

    private bool _initialized = false;

    private bool _showFormView { get; set; }

    private bool _isChartInitialized;

    [CascadingParameter]
    private RgfEntityComponent? ParentEntityComponent { get; set; }

    private RgfChartPresentationMode _chartPresentationMode;
    private bool IsChartWorkspace => _chartPresentationMode == RgfChartPresentationMode.Workspace;
    private bool _isTreeInitialized;
    private bool _pendingChartActivation;
    private RfgDisplayMode _previousDisplayMode = RfgDisplayMode.Grid;

    private string? EntityName { get; set; }

    private RenderFragment? _entityEditor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _logger.LogDebug("OnInitialized | EntityName:{EntityName}", EntityParameters.EntityName);

        await base.OnInitializedAsync();

        EntityName = EntityParameters.EntityName;
        _chartPresentationMode = !EntityParameters.ChartParameters.SupportsWorkspace || EntityParameters.IsEmbeddedEntity ||
            EntityParameters.ParentEntityParameters != null || ParentEntityComponent != null
            ? RgfChartPresentationMode.Dialog : RgfChartPresentationMode.Workspace;
        EntityParameters.ChartParameters.PresentationMode = _chartPresentationMode;

        try
        {
            DisposeManager();
            _createManagerCancellationTokenSource = new CancellationTokenSource();
            await CreateManagerAsync(_createManagerCancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("OnInitialized | OperationCanceledException | EntityName:{EntityName}", EntityParameters.EntityName);
        }
    }

    protected override void OnParametersSet()
    {
        _logger.LogDebug("OnParametersSet | EntityName:{EntityName}", EntityParameters.EntityName);

        base.OnParametersSet();
        EntityParameters.ChartParameters.PresentationMode = _chartPresentationMode;

        if (EntityName != EntityParameters.EntityName)
        {
            EntityName = EntityParameters.EntityName;
            Refresh(true);
        }
        else
        {
            EntityParameters.Manager = Manager;
        }
    }

    private async Task CreateManagerAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("CreateManager | EntityName:{EntityName} - {HashCode}", EntityParameters.EntityName, this.GetHashCode());

        var gridRequest = RgfGridRequest.Create(EntityParameters);
        gridRequest.EntityName = EntityParameters.EntityName;
        gridRequest.Skeleton = true;
        gridRequest.SkipInitialData = EntityParameters.SkipInitialDataLoad;
        gridRequest.SelectParam = EntityParameters.SelectParam;
        gridRequest.EntityKey = EntityParameters.FormParameters?.FormViewKey.EntityKey;
        gridRequest.ListParam = EntityParameters.ListParam;
        gridRequest.FilterParent = EntityParameters.FilterParent;
        gridRequest.CustomParams = EntityParameters.CustomParameters;
        if (EntityParameters.GridParameters?.InitialGridSettingsId.HasValue == true)
        {
            gridRequest.GridSettings = new RgfGridSettings
            {
                GridSettingsId = EntityParameters.GridParameters.InitialGridSettingsId
            };
        }

        Manager = new RgManager(gridRequest, _serviceProvider);
        Manager.RefreshEntity += Refresh;
        Manager.FormViewKey.OnAfterChange(this, OnChangeFormViewKey);
        Disposables.Add(Manager.NotificationManager.Subscribe<RgfUserMessageEventArgs>(OnUserMessage));
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(
            [RgfToolbarEventKind.Refresh, RgfToolbarEventKind.Add, RgfToolbarEventKind.Edit, RgfToolbarEventKind.Read, RgfToolbarEventKind.Delete, RgfToolbarEventKind.Select],
            OnGridToolbarCommandAsync, true);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.ToggleDisplayMode, OnToggleDisplayModeAsync, true);
        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.RecroChart, OnShowChart, true);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.RecroChart, OnShowChart, true);

        if (EntityParameters.DeferredInitialization)
        {
            EntityParameters.Manager = Manager;
        }
        else
        {
            var success = await ((RgManager)Manager).InitializeAsync(gridRequest);
            cancellationToken.ThrowIfCancellationRequested();

            if (success)
            {
                EntityParameters.Manager = Manager;
                if (EntityParameters.FormOnly || EntityParameters.AutoOpenForm)
                {
                    if (Manager.ListHandler.ItemCount.Value == 1)
                    {
                        var data = await Manager.ListHandler.GetDataListAsync();
                        cancellationToken.ThrowIfCancellationRequested();

                        var rowIndexAndKey = Manager.ListHandler.GetRowIndexAndKey(data[0]);
                        Manager.SelectedItems.Value = new Dictionary<int, RgfEntityKey> { { rowIndexAndKey.Key, rowIndexAndKey.Value } };
                    }
                    else if (EntityParameters.FormOnly)
                    {
                        EntityParameters.FormOnly = false;
                        _logger.LogError("formOnly => ItemCount={ItemCount}", Manager.ListHandler.ItemCount.Value);
                    }
                    if (Manager.SelectedItems.Value.Count == 1)
                    {
                        await Manager.OnToolbarCommandAsync(new RgfEventArgs<RgfToolbarEventArgs>(this, new(RgfToolbarEventKind.Read)));
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                NormalizeDisplayMode();
                await InitResourcesAsync();
                cancellationToken.ThrowIfCancellationRequested();
                _initialized = true;

                var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Initialized, Manager);
                await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Initialized | EntityName:{EntityName}", EntityParameters.EntityName);
            }
            else
            {
                var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Destroy, Manager);
                await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private Task OnToggleDisplayModeAsync(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        if (args.Args.Data?.TryGetValue(nameof(RgfToolbarEventKind.ToggleDisplayMode), out var mode) == true && mode is RfgDisplayMode displayMode)
        {
            if (displayMode == RfgDisplayMode.Tree && TreeTemplate == null)
            {
                return Task.CompletedTask;
            }
            if (displayMode == RfgDisplayMode.Chart)
            {
                return OnShowChart(args);
            }
            _isTreeInitialized |= displayMode == RfgDisplayMode.Tree;
            EntityParameters.DisplayMode = displayMode;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private Task OnGridToolbarCommandAsync(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        if (EntityParameters.DisplayMode == RfgDisplayMode.Chart && args.Args.EventKind != RgfToolbarEventKind.Refresh)
        {
            return Task.CompletedTask;
        }
        return Manager!.OnToolbarCommandAsync(args);
    }

    private Task OnShowChart(IRgfEventArgs args)
    {
        args.Handled = true;
        if (IsChartWorkspace && EntityParameters.DisplayMode == RfgDisplayMode.Chart)
        {
            EntityParameters.DisplayMode = _previousDisplayMode == RfgDisplayMode.Tree && TreeTemplate != null
                ? RfgDisplayMode.Tree : RfgDisplayMode.Grid;
            StateHasChanged();
            return Task.CompletedTask;
        }
        EnsureChartTemplate();
        if (ChartTemplate != null)
        {
            _isChartInitialized = true;
            if (IsChartWorkspace)
            {
                _previousDisplayMode = EntityParameters.DisplayMode == RfgDisplayMode.Tree ? RfgDisplayMode.Tree : RfgDisplayMode.Grid;
                EntityParameters.DisplayMode = RfgDisplayMode.Chart;
                _pendingChartActivation = true;
            }
        }
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void EnsureChartTemplate()
    {
        if (Manager?.EntityDesc.Options.GetBoolValue("RGO_ClientMode") == true)
        {
            return;
        }
        if (ChartTemplate == null && RgfBlazorConfiguration.TryGetComponentType(RgfBlazorConfiguration.ComponentType.Chart, out var chartType))
        {
            ChartTemplate = (par) => builder =>
            {
                int sequence = 0;
                builder.OpenComponent(sequence++, chartType!);
                builder.AddAttribute(sequence++, "EntityParameters", par);
                builder.CloseComponent();
            };
        }
    }

    private void NormalizeDisplayMode()
    {
        EntityParameters.DisplayMode ??= Enum.TryParse(Manager!.EntityDesc.Options.GetStringValue("RGO_DisplayMode"), out RfgDisplayMode mode)
            ? mode : RfgDisplayMode.Grid;
        if (EntityParameters.DisplayMode == RfgDisplayMode.Chart && IsChartWorkspace)
        {
            EnsureChartTemplate();
            _isChartInitialized = ChartTemplate != null;
            _pendingChartActivation = _isChartInitialized;
        }
        if ((EntityParameters.DisplayMode == RfgDisplayMode.Chart && (!IsChartWorkspace || !_isChartInitialized)) ||
            (EntityParameters.DisplayMode == RfgDisplayMode.Tree && TreeTemplate == null) ||
            !Enum.IsDefined(EntityParameters.DisplayMode ?? RfgDisplayMode.Grid))
        {
            EntityParameters.DisplayMode = RfgDisplayMode.Grid;
        }
        _isTreeInitialized |= EntityParameters.DisplayMode == RfgDisplayMode.Tree;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_pendingChartActivation && _initialized && EntityParameters.DisplayMode == RfgDisplayMode.Chart)
        {
            _pendingChartActivation = false;
            await EntityParameters.ChartParameters.EventDispatcher.RaiseEventAsync(RgfChartEventKind.WorkspaceActivated, this);
        }
    }

    private async Task InitResourcesAsync()
    {
        if (Manager?.EntityDesc.StylesheetsReferences?.Any() == true)
        {
            foreach (var css in Manager.EntityDesc.StylesheetsReferences)
            {
                await _jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{ApiService.BaseAddress}{css}", false);
            }
        }
    }

    private void Refresh(bool recreate)
    {
        _logger.LogDebug("Refresh | EntityName:{EntityName}", EntityParameters.EntityName);
        _ = InvokeAsync(() => RefreshAsync(recreate));
    }

    private async Task RefreshAsync(bool recreate)
    {
        try
        {
            if (recreate)
            {
                _initialized = false;
                _isChartInitialized = false;
                _isTreeInitialized = false;
                _pendingChartActivation = false;
                StateHasChanged();
                DisposeManager();
                _createManagerCancellationTokenSource = new CancellationTokenSource();
                await CreateManagerAsync(_createManagerCancellationTokenSource.Token);
            }
            _initialized = true;
            NormalizeDisplayMode();
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Initialized, Manager!);
            await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("OnInitialized | OperationCanceledException | EntityName:{EntityName}", EntityParameters.EntityName);
        }
    }

    protected virtual void OnUserMessage(IRgfEventArgs<RgfUserMessageEventArgs> args)
    {
        if (args.Handled != true && args.Args.Origin == UserMessageOrigin.Global)
        {
            _dynamicDialog.Dialog(args.Args);
            args.Handled = true;
        }
    }

    protected virtual void OnChangeFormViewKey(ObservablePropertyEventArgs<FormViewKey?> args)
    {
        _showFormView = args.NewData != null;
        EntityParameters.FormParameters.FormViewKey = args.NewData ?? new();
        if (EntityParameters.FormOnly && !_showFormView)
        {
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Destroy, Manager!);
            _ = EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
        }
    }

    public void Dispose()
    {
        EntityParameters?.UnsubscribeFromAll(this);
        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
        DisposeManager();
    }

    private void DisposeManager()
    {
        if (Manager != null)
        {
            EntityParameters?.UnsubscribeFromAll(this);
            _logger.LogDebug("Dispose Manager | EntityName:{EntityName} - {HashCode}", this.EntityName, this.GetHashCode());
            EntityParameters?.UnsubscribeFromAll(Manager);
            _createManagerCancellationTokenSource?.Cancel();
            Manager.Dispose();
            Manager = null;
        }
    }
}
