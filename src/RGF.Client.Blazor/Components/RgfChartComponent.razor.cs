using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfChartComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfChartComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    public EditContext EditContext { get; private set; } = new(new RgfAggregationSettings());

    public ValidationMessageStore MessageStore { get; private set; } = null!;

    private ConcurrentDictionary<string, string> _recroDictChart = [];

    public string GetRecroDictChart(string resourceKey, string? defaultValue = null) => _recroDict.GetItem(_recroDictChart, resourceKey, defaultValue);

    public List<RgfDynamicDictionary> DataColumns { get; set; } = [];

    public List<RgfDynamicDictionary> ChartData { get; set; } = [];

    public RenderFragment? ChartDataGrid { get; set; }

    private RgfEntityParameters ChartDataGridEntityParameters = null!;

    private bool _chartManagerInited;

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfChartParameters ChartParameters => EntityParameters.ChartParameters;

    public RgfChartSettings ChartSettings { get; set; } = new() { ShowDataLabels = true };

    public List<RgfChartSettings> ChartSettingList { get; private set; } = [];

    public bool IsPublicChartSettingAllowed => Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.PublicChartSetting);

    public IEnumerable<RgfProperty> AllowedProperties { get; private set; } = [];

    public Dictionary<int, string> ChartColumnsNumeric => AllowedProperties.Where(e => e.ListType == PropertyListType.Numeric || e.ClientDataType.IsNumeric()).OrderBy(e => e.ColTitle).ToDictionary(p => p.Id, p => p.ColTitle);

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private bool _showComponent = true;

    public RgfProcessingStatus DataStatus { get; private set; } = RgfProcessingStatus.Invalid;

    public RgfProcessingStatus ChartStatus { get; set; } = RgfProcessingStatus.Invalid;

    private RenderFragment? _chartDialog { get; set; }

    private bool _pendingParentRefresh;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _recroDictChart = await _recroDict.GetDictionaryAsync("RGF.UI.Chart", _recroSec.UserLanguage);

        var validFormTypes = new[] {
            PropertyFormType.TextBox,
            PropertyFormType.TextBoxMultiLine,
            PropertyFormType.CheckBox,
            PropertyFormType.DropDown,
            PropertyFormType.Date,
            PropertyFormType.DateTime,
            PropertyFormType.StaticText
        };

        AllowedProperties = Manager.EntityDesc.Properties
            .Where(p => p.Readable && !p.IsDynamic &&
                //p.Options?.GetStringValue("RGO_AutoExternal") == null &&
                p.Options?.GetBoolValue("RGO_AggregationExclude") != true &&
                (validFormTypes.Contains(p.FormType) || p.Options?.GetBoolValue("RGO_AggregationRequired") == true))
            .OrderBy(e => e.ColTitle)
            .ToArray();

        ChartSettings.AggregationSettings.Columns = new List<RgfAggregationColumn> { new() { Id = 0, Aggregate = "Count" } };

        EditContext = new(ChartSettings.AggregationSettings);
        EditContext.OnValidationRequested += HandleValidationRequested;
        MessageStore = new(EditContext);

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.RecroChart, OnShowChart);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.RecroChart, OnShowChart);

        ChartParameters.DialogParameters.Title = "RecroChart - " + Manager.EntityDesc.MenuTitle;
        ChartParameters.DialogParameters.UniqueName = "chart-" + Manager.EntityDesc.NameVersion.ToLower();
        ChartParameters.DialogParameters.EventDispatcher.Subscribe(RgfDialogEventKind.Close, OnDialogCloseAsync, true);
        ChartParameters.DialogParameters.IsInline = Embedded;
        ChartParameters.DialogParameters.NoHeader = Embedded;
        ChartParameters.DialogParameters.ShowCloseButton = !Embedded;
        ChartParameters.DialogParameters.ContentTemplate = ContentTemplate(this);
        ChartParameters.DialogParameters.FooterTemplate = Embedded ? null : FooterTemplate(this);
        ChartParameters.DialogParameters.PredefinedButtons = Embedded ? [] : null;
        ChartParameters.DialogParameters.Resizable ??= !Embedded;
        ChartParameters.DialogParameters.Height ??= Embedded ? "100%" : "700px";
        ChartParameters.DialogParameters.Width ??= Embedded ? "100%" : "1020px";
        ChartParameters.DialogParameters.MinWidth ??= Embedded ? null : "920px";

        if (EntityParameters.DialogTemplate != null)
        {
            _chartDialog = EntityParameters.DialogTemplate(ChartParameters.DialogParameters);
        }
        else
        {
            _chartDialog = RgfDynamicDialog.Create(ChartParameters.DialogParameters, _logger);
        }

        ChartSettingList = await Manager.GetChartSettingsListAsync();
        ChartDataGridEntityParameters = new RgfEntityParameters("RGRecroChart", Manager.SessionParams) { DeferredInitialization = true, ParentEntityParameters = EntityParameters };
        ChartDataGrid = RgfEntityComponent.Create(ChartDataGridEntityParameters);

        _logger.LogDebug("RgfChartComponentInitialized | EntityName:{EntityName}", EntityParameters.EntityName);
        var eventArg = new RgfEventArgs<RgfChartEventArgs>(this, new RgfChartEventArgs(RgfChartEventKind.Initialized));
        await ChartParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        var eventArgs = RgfChartEventArgs.CreateAfterRenderEvent(firstRender);
        await ChartParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfChartEventArgs>(this, eventArgs));
    }

    protected void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e) => Validation(MessageStore, ChartSettings);

    private void OnShowChart(IRgfEventArgs args)
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        _showComponent = true;
        args.Handled = true;
        args.PreventDefault = true;
        StateHasChanged();
        var eventArgs = new RgfChartEventArgs(RgfChartEventKind.ShowChart);
        _ = EntityParameters.ChartParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfChartEventArgs>(this, eventArgs));
    }

    private Task OnDialogCloseAsync(IRgfEventArgs<RgfDialogEventArgs> args) => CloseDialogAsync();

    private async Task CloseDialogAsync()
    {
        _chartManagerInited = false;
        _showComponent = false;
        await ChartParameters.DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Destroy, this);
        StateHasChanged();
    }

    public Task OnClose(MouseEventArgs? args) => ChartParameters.DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Close, this);

    public bool ConsumePendingParentRefresh()
    {
        if (!_pendingParentRefresh)
        {
            return false;
        }

        _pendingParentRefresh = false;
        return true;
    }

    public virtual async Task<bool> CreateChartDataAsyc()
    {
        SetDataStatus(RgfProcessingStatus.InProgress);

        ChartData = [];
        DataColumns = [];

        var chartManager = ChartDataGridEntityParameters.Manager!;
        if (!_chartManagerInited)
        {
            chartManager.CreateGridRequestCreated += (sender, e) =>
            {
                e.Request.EntityName = "RGRecroChart";
                e.Request.ParentGridRequest = Manager.ListHandler.CreateAggregateRequest(ChartSettings.AggregationSettings);
            };
            _chartManagerInited = true;
        }

        try
        {
            bool res = await chartManager.RecreateAsync();
            if (res != true)
            {
                SetDataStatus(RgfProcessingStatus.Invalid);
                return false;
            }

            var chartDataColumns = chartManager.ListHandler.DataColumns;
            var dataList = await chartManager.ListHandler.GetDataRangeAsync(0, chartManager.ListHandler.ItemCount.Value - 1);
            var aggregationSettings = ChartSettings.AggregationSettings;

            foreach (var item in aggregationSettings.Columns)
            {
                string alias;
                if (item.Aggregate == "Count")
                {
                    alias = "Count";
                }
                else
                {
                    var oprop = AllowedProperties.FirstOrDefault(e => e.Id == item.Id);
                    if (oprop == null)
                    {
                        continue;
                    }
                    alias = $"{oprop.Alias}_{item.Aggregate.Replace('-', '_')}";
                }
                var prop = chartManager.EntityDesc.Properties.FirstOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
                if (prop == null)
                {
                    continue;
                }
                int idx = Array.FindIndex(chartDataColumns, e => e == prop.ClientName);
                if (idx < 0)
                {
                    continue;
                }
                var dataCol = new RgfDynamicDictionary();
                dataCol.SetMember("Aggregate", item.Aggregate);
                dataCol.SetMember("Alias", prop.Alias);
                dataCol.SetMember("Index", idx);
                var name = item.Aggregate == "Count" ? _recroDict.GetRgfUiString("ItemCount") : prop.ColTitle;
                dataCol.SetMember("Name", name);
                DataColumns.Add(dataCol);
            }

            var order = new List<string>();
            foreach (var group in aggregationSettings.Groups.Concat(aggregationSettings.SubGroup))
            {
                var oprop = AllowedProperties.FirstOrDefault(e => e.Id == group.Id);
                if (oprop == null)
                {
                    continue;
                }
                string alias = oprop.Alias;
                var prop = chartManager.EntityDesc.Properties.FirstOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
                if (prop == null)
                {
                    continue;
                }
                int idx = Array.FindIndex(chartDataColumns, e => e == prop.ClientName);
                if (idx < 0)
                {
                    continue;
                }
                var dataCol = new RgfDynamicDictionary();
                dataCol.SetMember("Alias", prop.Alias);
                dataCol.SetMember("PropertyId", group.Id);
                dataCol.SetMember("Index", idx);
                dataCol.SetMember("Name", prop.ColTitle);
                DataColumns.Add(dataCol);
                order.Add(prop.Alias);
            }

            IOrderedEnumerable<RgfDynamicDictionary>? ordered = null;
            foreach (var item in order)
            {
                if (ordered == null)
                {
                    ordered = dataList.OrderBy(e => e.GetMember(item)?.ToString());
                }
                else
                {
                    ordered = ordered.ThenBy(e => e.GetMember(item)?.ToString());
                }
            }
            ChartData = ordered?.ToList() ?? dataList;
            DataStatus = RgfProcessingStatus.Valid;
            return true;
        }
        catch
        {
            SetDataStatus(RgfProcessingStatus.Invalid);
            return false;
        }
    }

    public void Validation(ValidationMessageStore messageStore, RgfChartSettings rgfChartSettings)
    {
        messageStore.Clear();
        var aggregationSettings = rgfChartSettings.AggregationSettings;
        var allowedPropertyIds = AllowedProperties.Select(property => property.Id).ToHashSet();
        if (aggregationSettings.Columns.Count == 0)
        {
            ChartSettings.AggregationSettings.Columns = new List<RgfAggregationColumn> { new() { Id = 0, Aggregate = "Count" } };
        }
        for (int i = aggregationSettings.Columns.Count - 1; i >= 0; i--)
        {
            var col = aggregationSettings.Columns[i];
            if (col.Aggregate == "Count")
            {
                col.Id = 0;
                for (int i2 = 0; i2 < i; i2++)
                {
                    if (aggregationSettings.Columns[i2].Aggregate == "Count")
                    {
                        messageStore.Add(() => col.Aggregate, "");
                    }
                }
            }
            else if (col.Id == 0)
            {
                messageStore.Add(() => col.Id, "");
            }
            else if (!allowedPropertyIds.Contains(col.Id))
            {
                messageStore.Add(() => aggregationSettings.Columns[i], "");
            }
        }
        if (rgfChartSettings.SeriesType == RgfChartSeriesType.Card)
        {
            if (aggregationSettings.Columns.Count > 1)
            {
                messageStore.Add(() => aggregationSettings.Columns[1], "");
            }
            if (aggregationSettings.SubGroup.Count > 0)
            {
                messageStore.Add(() => aggregationSettings.SubGroup[0], "");
            }
        }
        else if (rgfChartSettings.SeriesType != RgfChartSeriesType.Bar && rgfChartSettings.SeriesType != RgfChartSeriesType.Line)
        {
            if (aggregationSettings.Columns.Count > 1)
            {
                messageStore.Add(() => aggregationSettings.Columns[1], "");
            }
            if (aggregationSettings.SubGroup.Count > 0)
            {
                messageStore.Add(() => aggregationSettings.SubGroup[0], "");
            }
        }
        for (int i = aggregationSettings.SubGroup.Count - 1; i >= 0; i--)
        {
            var group = aggregationSettings.SubGroup[i];
            if (group.Id == 0 || !allowedPropertyIds.Contains(group.Id) || aggregationSettings.SubGroup.IndexOf(group) < i || aggregationSettings.Groups.IndexOf(group) != -1)
            {
                messageStore.Add(() => aggregationSettings.SubGroup[i], "");
            }
        }
        for (int i = aggregationSettings.Groups.Count - 1; i >= 0; i--)
        {
            var group = aggregationSettings.Groups[i];
            if (group.Id == 0 || !allowedPropertyIds.Contains(group.Id) || aggregationSettings.Groups.IndexOf(group) < i)
            {
                messageStore.Add(() => aggregationSettings.Groups[i], "");
            }
        }
    }

    public void SetDataStatus(RgfProcessingStatus status)
    {
        DataStatus = status;
        ChartStatus = RgfProcessingStatus.Invalid;
        StateHasChanged();
    }

    public void AddColumn()
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.Columns.Add(new() { Id = 0, Aggregate = "Sum" });
    }

    public void RemoveColumn(RgfAggregationColumn column)
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.Columns.Remove(column);
    }

    public void AddGroup()
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.Groups.Add(new());
    }

    public void RemoveAtGroup(int idx)
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.Groups.RemoveAt(idx);
    }

    public void AddSubGroup()
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.SubGroup.Add(new());
    }

    public void RemoveAtSubGroup(int idx)
    {
        SetDataStatus(RgfProcessingStatus.Invalid);
        ChartSettings.AggregationSettings.SubGroup.RemoveAt(idx);
    }

    public void Dispose()
    {
        EntityParameters?.UnsubscribeFromAll(this);
    }

    public virtual async Task<bool> OnSetChartSettingAsync(int? chartSettingsId, string name)
    {
        _logger.LogDebug("OnSetChartSetting | {id}:{name}", chartSettingsId, name);
        _pendingParentRefresh = false;
        if (chartSettingsId > 0)
        {
            var gs = ChartSettingList.FirstOrDefault(e => e.ChartSettingsId == chartSettingsId);
            if (gs?.ChartSettingsId > 0)
            {
                ChartSettings = RgfChartSettings.DeepCopy(gs);
                var parentGridSettings = ChartSettings.ParentGridSettings ?? new RgfGridSettings();
                var conditions = parentGridSettings.Conditions ?? [];
                var filterHandler = await Manager.GetFilterHandlerAsync();
                filterHandler.ApplyFilterState(conditions, parentGridSettings.SQLTimeout);
                _pendingParentRefresh = !Embedded;
                await Manager.ToastManager.RaiseEventAsync(new RgfToastEventArgs(Manager.EntityDesc.MenuTitle, RgfToastEventArgs.ActionTemplate(_recroDict.GetRgfUiString("Settings"), ChartSettings.SettingsName), delay: 2000), this);
                return true;
            }
        }

        ChartSettings = RgfChartSettings.DeepCopy(ChartSettings);
        ChartSettings.SettingsName = name;
        ChartSettings.ChartSettingsId = null;
        ChartSettings.IsReadonly = false;
        return false;
    }

    public virtual async Task<bool> SaveChartSettingsAsync()
    {
        var res = await Manager.SaveChartSettingsAsync(ChartSettings);
        if (res != null)
        {
            ChartSettings.RoleId = res.RoleId;
            if (ChartSettings.ChartSettingsId == null || ChartSettings.ChartSettingsId == 0)
            {
                ChartSettings.ChartSettingsId = res.ChartSettingsId;
            }
            ChartSettingList.RemoveAll(e => e.ChartSettingsId == ChartSettings.ChartSettingsId);
            ChartSettingList.Insert(0, RgfChartSettings.DeepCopy(ChartSettings));
            return true;
        }
        return false;
    }

    public void OnDeleteChartSettingsAsync() => _dynamicDialog.PromptDeletionConfirmation(DeleteChartSettingsAsync, $"{_recroDict.GetRgfUiString("Chart")}: {ChartSettings.SettingsName}");

    public virtual async Task<bool> DeleteChartSettingsAsync()
    {
        if (ChartSettings.ChartSettingsId != null && ChartSettings.ChartSettingsId != 0)
        {
            var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, _recroDict.GetRgfUiString("Delete"), ChartSettings.SettingsName);
            await Manager.ToastManager.RaiseEventAsync(toast, this);
            bool res = await Manager.DeleteChartSettingsAsync((int)ChartSettings.ChartSettingsId);
            if (res)
            {
                ChartSettingList = ChartSettingList.Where(e => e.ChartSettingsId != ChartSettings.ChartSettingsId).ToList();
                ChartSettings.ChartSettingsId = null;
                ChartSettings.SettingsName = ""; //clear text input
                await Manager.ToastManager.RaiseEventAsync(toast.Recreate(_recroDict.GetRgfUiString("Processed"), RgfToastType.Info), this);
                StateHasChanged();
                return true;
            }
        }
        return false;
    }

    public RgfChartCardModel? CreateCardModel()
    {
        if (ChartSettings.SeriesType != RgfChartSeriesType.Card
            || DataStatus != RgfProcessingStatus.Valid
            || ChartData.Count != 1)
        {
            return null;
        }

        RgfDynamicDictionary? aggregateColumn = null;
        foreach (var column in DataColumns)
        {
            if (string.IsNullOrWhiteSpace(column.Get<string?>("Aggregate")))
            {
                continue;
            }

            if (aggregateColumn != null)
            {
                return null;
            }

            aggregateColumn = column;
        }

        if (aggregateColumn == null)
        {
            return null;
        }

        var row = ChartData[0];
        var alias = aggregateColumn.Get<string>("Alias");
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        var data = row.GetItemData(alias);
        if (data == null)
        {
            return null;
        }

        var value = FormatCardValue(data);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var aggregate = aggregateColumn.Get<string?>("Aggregate");
        var title = aggregateColumn.Get<string>("Name");
        return new RgfChartCardModel
        {
            Title = title,
            Value = value,
            Remark = string.IsNullOrWhiteSpace(ChartSettings.Remark) ? null : ChartSettings.Remark.Trim()
        };
    }

    private static string FormatCardValue(RgfDynamicData data)
    {
        var decimalValue = data.DecimalValue
            ?? data.TryGetDecimal(CultureInfo.CurrentCulture)
            ?? data.TryGetDecimal(CultureInfo.InvariantCulture);

        if (decimalValue != null)
        {
            return decimalValue.Value.ToString("#,0.################", CultureInfo.CurrentCulture);
        }

        return data.ToString() ?? string.Empty;
    }
}
