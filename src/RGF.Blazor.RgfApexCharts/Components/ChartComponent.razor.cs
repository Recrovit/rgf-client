using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ChartComponent : ComponentBase
{
    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfChartComponent _rgfChartRef { get; set; } = null!;

    private ApexChart<ChartSerieData> _chartRef { get; set; } = null!;

    private ApexChartOptions<ChartSerieData> _options { get; set; } = new();

    private EditContext _editContext = null!;

    private ValidationMessageStore _messageStore = null!;

    [SupplyParameterFromForm]
    private RgfChartParam Model { get; set; } = new();

    public Dictionary<int, string>? ChartColumnsNumeric { get; set; }

    private List<ChartSerie> _series = [];

    private string? _title;

    private SeriesType _seriesType = SeriesType.Bar;

    private bool _isCount => Model.Columns[0].Aggregate?.Equals("Count", StringComparison.OrdinalIgnoreCase) == true;

    private bool _stacked = false;

    private bool _horizontal = false;

    private int? _height;

    private int? _width;

    protected override void OnInitialized()
    {
        Model.Take = 100;

        _editContext = new(Model);
        _editContext.OnValidationRequested += HandleValidationRequested;
        _messageStore = new(_editContext);

        _options = new()
        {
            Theme = new Theme { Mode = Mode.Light, Palette = PaletteType.Palette1 },
            Chart = new() { Stacked = _stacked },
            NoData = new NoData { Text = "No Data..." },
            PlotOptions = new PlotOptions()
            {
                Bar = new PlotOptionsBar()
                {
                    Horizontal = _horizontal,
                    DataLabels = new PlotOptionsBarDataLabels { Total = new BarTotalDataLabels { Style = new BarDataLabelsStyle { FontWeight = "800" } } }
                }
            },
            DataLabels = new DataLabels
            {
                Enabled = true,
                Formatter = "function (value) { return value.toLocaleString(); }"
            },
            Yaxis = new List<YAxis>()
            {
                new YAxis { Labels = new YAxisLabels { Formatter = "function (value) { return value.toLocaleString(); }" } }
            }
        };

        Model.Columns = new List<RgfGroupColumn>
        {
            new() { PropertyId = 0, Aggregate = "Count" },
            new() { PropertyId = 0, Aggregate = string.Empty },
        };
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            ChartColumnsNumeric = _rgfChartRef.ChartColumns.Where(e => e.ListType == PropertyListType.Numeric || e.ClientDataType.IsNumeric()).OrderBy(e => e.ColTitle).ToDictionary(p => p.Id, p => p.ColTitle);
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _messageStore.Clear();
        var prop1 = Model.Columns[0];
        var prop2 = Model.Columns[1];
        if (Model.Columns[0].PropertyId == 0)
        {
            _messageStore.Add(() => prop1.PropertyId, "");
        }
        if (prop2.PropertyId == 0 && !_isCount ||
            prop1.PropertyId == prop2.PropertyId)
        {
            _messageStore.Add(() => prop2.PropertyId, "");
        }
    }

    private async Task Submit()
    {
        _series.Clear();
        var prop1 = Model.Columns[0];
        var prop2 = Model.Columns[1];

        var res = await _rgfChartRef.CreateChartDataAsyc(Model);
        if (res == null)
        {
            return;
        }

        _title = $"{Manager.EntityDesc.Title} : ";
        for (int i = 0; i < Model.Columns.Count; i++)
        {
            var colTitle = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Id == Model.Columns[i].PropertyId)?.ColTitle;
            if (i == 1)
            {
                _title += " / ";
            }
            if (i > 1)
            {
                _title += ", ";
            }
            if (!string.IsNullOrEmpty(colTitle))
            {
                _title += i == 0 || _isCount ? colTitle : $"{Model.Columns[i].Aggregate}({colTitle})";
            }
            else if (_isCount)
            {
                _title += "Count";
            }
        }

        int yProp = 0;
        int xProp = 1;
        if (_isCount)
        {
            if (_rgfChartRef.DataColumns.Length > 2)
            {
                var group1arr = _rgfChartRef.ChartData.GroupBy(e => e.GetItemData(_rgfChartRef.DataColumns[1]).StringValue).Select(g => new { Group = g.Key })
                    .OrderBy(e => e.Group)
                    .ToArray();

                var group2arr = _rgfChartRef.ChartData.GroupBy(e => e.GetItemData(_rgfChartRef.DataColumns[2]).StringValue).Select(g => new { Group = g.Key })
                    .OrderBy(e => e.Group)
                    .ToArray();

                foreach (var group2 in group2arr)
                {
                    var g2data = _rgfChartRef.ChartData.Where(e => group2.Group == e.GetItemData(_rgfChartRef.DataColumns[2]).StringValue).ToArray();
                    var serie = new ChartSerie();
                    var prop = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Alias.Equals(group2.Group, StringComparison.OrdinalIgnoreCase));
                    if (prop == null)
                    {
                        prop = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Alias.Equals(Regex.Replace(group2.Group, @"\d+$", ""), StringComparison.OrdinalIgnoreCase));
                    }
                    serie.Name = prop?.ColTitle ?? group2.Group;
                    foreach (var group1 in group1arr)
                    {
                        var cd = new ChartSerieData { X = group1.Group, Y = 0 };
                        var data = g2data.SingleOrDefault(e => group1.Group == e.GetItemData(_rgfChartRef.DataColumns[1]).StringValue);
                        if (data != null)
                        {
                            cd.Y = data.GetItemData(_rgfChartRef.DataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0;
                        }
                        serie.Data.Add(cd);
                    }
                    _series.Add(serie);
                }
            }
            else
            {
                var chart = new ChartSerie();
                chart.Name = "Count";
                chart.Data = _rgfChartRef.ChartData.Select(e => new ChartSerieData
                {
                    X = e.GetItemData(_rgfChartRef.DataColumns[xProp]).StringValue,
                    Y = e.GetItemData(_rgfChartRef.DataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0
                }).OrderBy(e => e.X).ToList();
                _series.Add(chart);
            }
        }
        else
        {
            var g1 = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Id == prop1.PropertyId)?.Alias;
            if (g1 != null)
            {
                xProp = _rgfChartRef.DataColumns.ToList().FindIndex(e => string.Equals(e, g1, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                xProp = 0;
            }
            for (yProp = 0; yProp < _rgfChartRef.DataColumns.Length; yProp++)
            {
                if (yProp == xProp)
                {
                    continue;
                }
                var serie = new ChartSerie();
                var prop = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Alias.Equals(_rgfChartRef.DataColumns[yProp], StringComparison.OrdinalIgnoreCase));
                if (prop == null)
                {
                    prop = _rgfChartRef.ChartColumns.SingleOrDefault(e => e.Alias.Equals(Regex.Replace(_rgfChartRef.DataColumns[yProp], @"\d+$", ""), StringComparison.OrdinalIgnoreCase));
                }
                serie.Name = prop?.ColTitle ?? _rgfChartRef.DataColumns[yProp];
                serie.Data = _rgfChartRef.ChartData.Select(e => new ChartSerieData
                {
                    X = e.GetItemData(_rgfChartRef.DataColumns[xProp]).StringValue,
                    Y = e.GetItemData(_rgfChartRef.DataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0
                }).OrderBy(e => e.X).ToList();
                _series.Add(serie);
            }
        }

        StateHasChanged();
        await _chartRef.UpdateSeriesAsync(true);
        await _chartRef.UpdateOptionsAsync(true, true, true);
    }

    private async Task UpdateSize()
    {
        await _chartRef.UpdateOptionsAsync(true, true, false);
    }

    private async Task UpdateChart()
    {
        await _chartRef.RenderAsync();
        StateHasChanged();
    }

    private void OnChangeAggregate(string value, RgfGroupColumn column, int idx)
    {
        column.Aggregate = value;
        _messageStore.Clear();
        if (idx == 0)
        {
            if (column.Aggregate?.Equals("Count", StringComparison.OrdinalIgnoreCase) == true)
            {
                Model.Columns[1].Aggregate = null;
                for (int i = Model.Columns.Count - 1; i > 1; i++)
                {
                    Model.Columns.RemoveAt(i);
                }
            }
            else
            {
                Model.Columns[1].Aggregate = "Sum";
            }
            Model.Columns[1].PropertyId = 0;
        }
        StateHasChanged();
    }

    private void AddColumn(MouseEventArgs e)
    {
        Model.Columns.Add(new RgfGroupColumn { PropertyId = 0, Aggregate = "Sum" });
        StateHasChanged();
    }

    private void RemoveColumn(RgfGroupColumn column)
    {
        Model.Columns.Remove(column);
        StateHasChanged();
    }
}