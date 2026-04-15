using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using System.Data;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ApexChartComponent : ComponentBase
{
    [Inject]
    private ILogger<RgfChartComponent> _logger { get; set; } = null!;

    private ApexChart<ChartSerieData> _chartRef { get; set; } = null!;

    private List<string> xData = [];
    private List<string> xAlias = [];
    private List<string> sgData = [];
    private List<string> sgAlias = [];

    public async Task UpdateChart()
    {
        _logger.LogDebug("UpdateChart");
        StateHasChanged();
        await Task.Delay(50);
        //await _chartRef.UpdateOptionsAsync(true, true, true);
        //await _chartRef.UpdateSeriesAsync(true);
        await _chartRef.RenderAsync();
    }

    public async Task RenderChartAsync(string title, string chartName, RgfAggregationSettings aggregationSettings, List<RgfDynamicDictionary> dataColumns, IEnumerable<RgfDynamicDictionary> chartData)
    {
        var columns = new List<string>();
        ChartSettings.Series.Clear();
        ChartSettings.Title = string.IsNullOrEmpty(title) ? chartName : title;

        xAlias = [];
        foreach (var group in aggregationSettings.Groups)
        {
            for (int i = 0; i < dataColumns.Count; i++)
            {
                var propertyId = dataColumns[i].GetItemData("PropertyId")?.IntValue;
                if (propertyId == group.Id)
                {
                    var alias = dataColumns[i].Get<string>("Alias");
                    xAlias.Add(alias);
                    break;
                }
            }
        }
        xData = chartData.GroupBy(arr => string.Join(" / ", xAlias.Select(alias => arr.GetMember(alias)?.ToString() ?? alias))).Select(e => e.Key).ToList();

        sgAlias = [];
        foreach (var group in aggregationSettings.SubGroup)
        {
            for (int i = 0; i < dataColumns.Count; i++)
            {
                var propertyId = dataColumns[i].GetItemData("PropertyId")?.IntValue;
                if (propertyId == group.Id)
                {
                    var alias = dataColumns[i].Get<string>("Alias");
                    sgAlias.Add(alias);
                    break;
                }
            }
        }
        sgData = chartData.GroupBy(arr => string.Join(" / ", sgAlias.Select(alias => arr.GetMember(alias)?.ToString() ?? alias))).Select(e => e.Key).OrderBy(e => e).ToList();
        var cultureInfo = new System.Globalization.CultureInfo("en");

        for (int i = 0; i < dataColumns.Count; i++)
        {
            var acolumn = dataColumns[i];
            var name = acolumn.Get<string>("Name");
            var aggregate = acolumn.Get<string?>("Aggregate");
            if (string.IsNullOrEmpty(aggregate))
            {
                continue;
            }
            var dataAlias = acolumn.Get<string>("Alias");
            if (string.IsNullOrEmpty(title))
            {
                if (i > 0)
                {
                    ChartSettings.Title += ", ";
                }
                if (!string.IsNullOrEmpty(name))
                {
                    ChartSettings.Title += name;
                }
            }
            if (aggregate != "Count")
            {
                name = $"{aggregate}({name})";
            }
            if (aggregationSettings.SubGroup.Count == 0)
            {
                var data = chartData.ToDictionary(e => string.Join(" / ", xAlias.Select(alias => e.GetMember(alias)?.ToString() ?? alias)), v => v);
                AddSerie(data, name, dataAlias, cultureInfo);
            }
            else
            {
                foreach (var item in sgData)
                {
                    var data = chartData.Where(e => string.Join(" / ", sgAlias.Select(alias => e.GetMember(alias)?.ToString() ?? alias)) == item)
                        .ToDictionary(e => string.Join(" / ", xAlias.Select(alias => e.GetMember(alias)?.ToString() ?? alias)), v => v);
                    AddSerie(data, $"{item}: {name}", dataAlias, cultureInfo);
                }
            }
        }
        await UpdateChart();
    }

    private void AddSerie(Dictionary<string, RgfDynamicDictionary> chartData, string name, string dataAlias, CultureInfo cultureInfo)
    {
        _logger.LogDebug("AddSerie | {name}", name);
        var serie = new ChartSerie()
        {
            Name = name,
            Data = []
        };
        foreach (var item in xData)
        {
            var data = chartData.TryGetValue(item, out var chartEntry) ? chartEntry : null;
            var sd = new ChartSerieData()
            {
                Y = data?.GetItemData(dataAlias).TryGetDecimal(cultureInfo) ?? 0
            };
            if (xAlias.Count > 1 &&
                (ChartSettings.SeriesType == SeriesType.Bar || ChartSettings.SeriesType == SeriesType.Line) &&
                data != null)
            {
                sd.X = xAlias.Select(alias => data.GetMember(alias)?.ToString() ?? "").ToArray();
            }
            else
            {
                sd.X = item ?? "";
            }
            serie.Data.Add(sd);

        }
        ChartSettings.Series.Add(serie);
    }
}