using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public enum RecroChartTab
{
    Settings = 1,
    Chart = 2,
    Grid = 3
}

public abstract class BaseChartComponent : ComponentBase
{
    [Parameter, EditorRequired]
    public RgfEntityParameters EntityParameters { get; set; } = null!;

    public BaseChartComponent()
    {
        _id++;
        ContainerId = $"rgf-apexchart-{_id}";
        ApexChartSettings = new()
        {
            Options = new()
            {
                Theme = new Theme
                {
                    Mode = Mode.Light,
                    Palette = PaletteType.Palette1
                },
                Chart = new()
                {
                    Stacked = false,
                    Toolbar = new() { Show = true }
                },
                NoData = new NoData { Text = "No Data..." },
                PlotOptions = new PlotOptions()
                {
                    Bar = new PlotOptionsBar()
                    {
                        Horizontal = false,
                        DataLabels = new PlotOptionsBarDataLabels { Total = new BarTotalDataLabels { Style = new BarDataLabelsStyle { FontWeight = "800" } } }
                    }
                },
                DataLabels = new DataLabels
                {
                    Enabled = true,
                    Formatter = "function (value) { return Array.isArray(value) ? value.join('/') : value?.toLocaleString(); }"
                },
                Yaxis = new List<YAxis>()
                {
                    new YAxis { Labels = new YAxisLabels { Formatter = "function (value) { return Array.isArray(value) ? value.join('/') : value?.toLocaleString(); }" } }
                }
            }
        };
    }

    [Inject]
    protected IJSRuntime _jsRuntime { get; init; } = default!;

    [Inject]
    protected IRecroDictService RecroDict { get; init; } = null!;

    protected RgfChartComponent RgfChartRef { get; set; } = null!;

    protected ApexChartComponent ApexChartRef { get; set; } = null!;

    protected DotNetObjectReference<BaseChartComponent>? _selfRef;

    private static int _id = 0;

    protected readonly string ContainerId;

    protected RecroChartTab ActiveTabIndex { get; set; } = RecroChartTab.Grid;

    protected bool SettingsAccordionActive { get; set; } = true;

    protected IRgManager Manager => EntityParameters.Manager!;

    protected RgfChartSettings _chartSettings => RgfChartRef.ChartSettings;

    protected ApexChartSettings ApexChartSettings { get; set; }

    protected override void OnInitialized()
    {
        EntityParameters.ChartParameters.EventDispatcher.Subscribe(RgfChartEventKind.ShowChart, (arg) => OnInitSize(true));
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            Initialize(_chartSettings);
        }
    }

    protected void Initialize(RgfChartSettings chartSetting)
    {
        ApexChartSettings.Options.Theme.Mode = Enum.TryParse(chartSetting.Theme, out Mode mode) ? mode : Mode.Light;
        ApexChartSettings.Options.Theme.Palette = Enum.TryParse(chartSetting.Palette, out PaletteType palette) ? palette : PaletteType.Palette1;
        ApexChartSettings.Options.Chart.Stacked = chartSetting.Stacked;
        ApexChartSettings.Options.PlotOptions.Bar.Horizontal = chartSetting.Horizontal;
        ChangeChartType(chartSetting.SeriesType);
        ChangedShowDataLabels(chartSetting.ShowDataLabels);
        ChangedLegend(chartSetting.Legend);
        ApexChartSettings.Width = chartSetting.Width;
        ApexChartSettings.Height = chartSetting.Height;
        ApexChartSettings.Title = "";
        ApexChartSettings.Series.Clear();
    }

    protected virtual async Task OnInitSize(bool recreate = false)
    {
        if (recreate)
        {
            SettingsAccordionActive = true;
            Initialize(_chartSettings);
        }
        if (!recreate && _selfRef != null)
        {
            await _jsRuntime.InvokeVoidAsync($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.resize", ContainerId, _selfRef, ApexChartSettings.Width, ApexChartSettings.Height);
        }
        else
        {
            bool inited = false;
            var jquiVer = await RgfBlazorConfiguration.ChkJQueryUiVer(_jsRuntime);
            if (jquiVer >= 0)
            {
                _selfRef ??= DotNetObjectReference.Create(this);
                inited = await _jsRuntime.InvokeAsync<bool>($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.initialize", ContainerId, _selfRef);
                if (inited && (ApexChartSettings.Width == null || ApexChartSettings.Height == null))
                {
                    await _jsRuntime.InvokeVoidAsync($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.resize", ContainerId, _selfRef, ApexChartSettings.Width, ApexChartSettings.Height);
                }
            }
            if (!inited)
            {
                _selfRef = null;
                ApexChartSettings.Height = 200;
            }
        }
    }

    [JSInvokable]
    public Task OnResize(int width, int height) => Resize(width, height);

    public virtual async Task Resize(int width, int height)
    {
        if (ActiveTabIndex != RecroChartTab.Chart || RgfChartRef == null)
        {
            return;
        }
        ApexChartSettings.Width = width < 1 ? null : Math.Max(width, 100);
        ApexChartSettings.Height = height < 1 ? null : Math.Max(height - 50, 100);
        RgfChartRef.ChartSettings.Width = ApexChartSettings.Width;
        RgfChartRef.ChartSettings.Height = ApexChartSettings.Height;
        StateHasChanged();
        if (RgfChartRef.DataStatus == RgfProcessingStatus.Valid)
        {
            await UpdateChart();
        }
    }

    protected async Task OnRedraw()
    {
        bool change = ActiveTabIndex != RecroChartTab.Chart || SettingsAccordionActive;
        if (change)
        {
            SettingsAccordionActive = false;
            ActiveTabIndex = RecroChartTab.Chart;
            StateHasChanged();
            await Task.Delay(1000);
        }
        await UpdateChart();
    }

    protected virtual async Task<bool> OnCreateChart()
    {
        if (RgfChartRef?.EditContext.Validate() != true)
        {
            return false;
        }
        SettingsAccordionActive = false;
        ActiveTabIndex = RecroChartTab.Chart;
        StateHasChanged();
        await Task.Delay(50);
        await OnInitSize();
        if (RgfChartRef.DataStatus == RgfProcessingStatus.Valid || await GetData())
        {
            await UpdateChart();
            return true;
        }
        return false;
    }

    protected virtual async Task<bool> OnGetData()
    {
        if (RgfChartRef?.EditContext.Validate() != true)
        {
            return false;
        }
        SettingsAccordionActive = false;
        ActiveTabIndex = RecroChartTab.Grid;
        return await GetData();
    }

    protected async Task<bool> GetData()
    {
        if (RgfChartRef.DataStatus == RgfProcessingStatus.Valid)
        {
            return false;
        }
        var toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, RgfChartRef.GetRecroDictChart("DataSet"), delay: 0);
        await Manager.ToastManager.RaiseEventAsync(toast, this);

        var success = await RgfChartRef.CreateChartDataAsyc();
        if (!success)
        {
            // Switch to this tab because the error message appears here
            ActiveTabIndex = RecroChartTab.Grid;
            await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RemoveToast(toast), this);
            return false;
        }
        await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success, delay: 2000), this);
        ApexChartSettings.Title = "";
        ApexChartSettings.Series.Clear();
        await ApexChartRef.UpdateChart();
        StateHasChanged();
        return true;
    }

    protected virtual async Task UpdateChart()
    {
        var currentStatus = RgfChartRef.ChartStatus;
        if (ApexChartRef == null || currentStatus == RgfProcessingStatus.InProgress)
        {
            return;
        }

        if (RgfChartRef?.DataStatus != RgfProcessingStatus.Valid)
        {
            await Manager.ToastManager.RaiseEventAsync(new RgfToastEvent(RecroDict.GetRgfUiString("Warning"), RecroDict.GetRgfUiString("InvalidState"), RgfToastType.Warning), this);
            return;
        }

        RgfChartRef.ChartStatus = RgfProcessingStatus.InProgress;// Prevents the chart from being redrawn when the data is updated
        try
        {
            RgfToastEvent toast;
            if (currentStatus == RgfProcessingStatus.Invalid)
            {
                toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, "Render", delay: 0);
                await Manager.ToastManager.RaiseEventAsync(toast, this);
                StateHasChanged();
                await Task.Delay(50);
                await ApexChartRef.RenderChartAsync(_chartSettings.SettingsName, $"{Manager.EntityDesc.Title} : ", _chartSettings.AggregationSettings, RgfChartRef.DataColumns, RgfChartRef.ChartData);
            }
            else
            {
                toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, RecroDict.GetRgfUiString("Redraw"), delay: 0);
                await Manager.ToastManager.RaiseEventAsync(toast, this);
                await ApexChartRef.UpdateChart();
            }
            await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success, 2000), this);
            RgfChartRef.ChartStatus = RgfProcessingStatus.Valid;
        }
        catch
        {
            RgfChartRef.ChartStatus = RgfProcessingStatus.Invalid;
        }
    }

    protected Task TryUpdateChart(object? args = null)
    {
        if (RgfChartRef?.ChartStatus == RgfProcessingStatus.Valid)
        {
            return UpdateChart();
        }
        return Task.CompletedTask;
    }

    protected Task ChangeChartType(RgfChartSeriesType seriesType)
    {
        _chartSettings.SeriesType = seriesType;
        switch (seriesType)
        {
            case RgfChartSeriesType.Bar:
                ApexChartSettings.SeriesType = SeriesType.Bar;
                break;

            case RgfChartSeriesType.Line:
                ApexChartSettings.SeriesType = SeriesType.Line;
                break;

            case RgfChartSeriesType.Pie:
                ApexChartSettings.SeriesType = SeriesType.Pie;
                break;

            case RgfChartSeriesType.Donut:
                ApexChartSettings.SeriesType = SeriesType.Donut;
                break;
        }
        return TryUpdateChart();
    }

    protected Task ChangedStacked(bool value)
    {
        _chartSettings.Stacked = value;
        ApexChartSettings.Options.Chart.Stacked = value;
        return TryUpdateChart();
    }

    protected Task ChangedHorizontal(bool value)
    {
        _chartSettings.Horizontal = value;
        ApexChartSettings.Options.PlotOptions.Bar.Horizontal = value;
        return TryUpdateChart();
    }

    protected Task ChangedShowDataLabels(bool value)
    {
        _chartSettings.ShowDataLabels = value;
        ApexChartSettings.ShowDataLabels = value;
        return TryUpdateChart();
    }

    protected Task ChangedLegend(bool value)
    {
        _chartSettings.Legend = value;
        ApexChartSettings.Options.Legend = !value ? default : new Legend
        {
            Formatter = @"function(seriesName, opts) { return [seriesName, ' - ', opts.w.globals.series[opts.seriesIndex].toLocaleString()] }"
        };
        return TryUpdateChart();
    }

    protected Task ChangeTheme(Mode? value)
    {
        _chartSettings.Theme = value?.ToString();
        ApexChartSettings.Options.Theme.Mode = value;
        return TryUpdateChart();
    }

    protected Task ChangePalette(PaletteType? value)
    {
        _chartSettings.Palette = value?.ToString();
        ApexChartSettings.Options.Theme.Palette = value;
        return TryUpdateChart();
    }

    protected void ChangedWidth(int? value)
    {
        _chartSettings.Width = value;
        ApexChartSettings.Width = value;
    }

    protected void ChangedHeight(int? value)
    {
        _chartSettings.Height = value;
        ApexChartSettings.Height = value;
    }

    protected virtual void OnTabActivated(RecroChartTab tab)
    {
        ActiveTabIndex = tab;
    }

    protected virtual void OnSettingsAccordionToggled()
    {
        SettingsAccordionActive = !SettingsAccordionActive;
    }

    protected async Task OnChartSettingsChanged(KeyValuePair<object?, string> arg)
    {
        var res = await RgfChartRef.OnSetChartSettingAsync(arg.Key?.ToString(), arg.Value);
        if (res)
        {
            RgfChartRef.SetDataStatus(RgfProcessingStatus.Invalid);
            SettingsAccordionActive = true;
            Initialize(_chartSettings);
            StateHasChanged();
            await OnGetData();
        }
    }
}