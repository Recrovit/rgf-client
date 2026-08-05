using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Formatting;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ApexChartComponent : ComponentBase
{
    [Inject]
    private ILogger<RgfChartComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    private ApexChart<ChartSerieData> _chartRef { get; set; } = null!;

    private List<AxisValueDescriptor> _xData = [];
    private List<AxisFieldDescriptor> _xAxisFields = [];
    private List<AxisValueDescriptor> _sgData = [];
    private List<AxisFieldDescriptor> _sgAxisFields = [];

    public async Task UpdateChart()
    {
        _logger.LogDebug("UpdateChart");
        StateHasChanged();
        await Task.Delay(50);
        if (ChartSettings.ChartType == RgfChartSeriesType.Card)
        {
            return;
        }
        await _chartRef.RenderAsync();
    }

    public async Task RenderChartAsync(RgfAggregationSettings aggregationSettings, List<RgfDynamicDictionary> dataColumns, IEnumerable<RgfDynamicDictionary> chartData)
    {
        ChartSettings.Series.Clear();
        ChartSettings.Card = null;

        var cultureInfo = _recroSec.UserCultureInfo();

        _xAxisFields = ResolveAxisFields(aggregationSettings.Groups, dataColumns);
        _xData = DistinctAxisValues(chartData, _xAxisFields, cultureInfo);

        _sgAxisFields = ResolveAxisFields(aggregationSettings.SubGroup, dataColumns);
        _sgData = DistinctAxisValues(chartData, _sgAxisFields, cultureInfo);

        ChartSettings.Options.Xaxis.Type = XAxisType.Category;

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
            if (aggregate != "Count")
            {
                name = $"{aggregate}({name})";
            }

            if (aggregationSettings.SubGroup.Count == 0)
            {
                var data = chartData.ToDictionary(e => CreateAxisValue(e, _xAxisFields, cultureInfo).Key, v => v);
                AddSerie(data, name, dataAlias, cultureInfo);
            }
            else
            {
                foreach (var item in _sgData)
                {
                    var data = chartData
                        .Where(e => CreateAxisValue(e, _sgAxisFields, cultureInfo).Key == item.Key)
                        .ToDictionary(e => CreateAxisValue(e, _xAxisFields, cultureInfo).Key, v => v);

                    AddSerie(data, $"{item.Label}: {name}", dataAlias, cultureInfo);
                }
            }
        }

        await UpdateChart();
    }

    public async Task RenderCardAsync(RgfChartCardModel card)
    {
        ChartSettings.Series.Clear();
        ChartSettings.Card = card;
        await UpdateChart();
    }

    private List<AxisFieldDescriptor> ResolveAxisFields(IEnumerable<RgfIdAliasPair> groups, List<RgfDynamicDictionary> dataColumns)
    {
        var fields = new List<AxisFieldDescriptor>();

        foreach (var group in groups)
        {
            for (int i = 0; i < dataColumns.Count; i++)
            {
                var propertyId = dataColumns[i].GetItemData("PropertyId")?.IntValue;
                if (propertyId == group.Id)
                {
                    var alias = dataColumns[i].Get<string>("Alias");
                    fields.Add(new AxisFieldDescriptor(alias, dataColumns[i].GetMember("Property") as IRgfProperty));
                    break;
                }
            }
        }

        return fields;
    }

    private void AddSerie(Dictionary<string, RgfDynamicDictionary> chartData, string name, string dataAlias, CultureInfo cultureInfo)
    {
        _logger.LogDebug("AddSerie | {name}", name);
        var serie = new ChartSerie()
        {
            Name = name,
            Data = []
        };

        foreach (var item in _xData)
        {
            var data = chartData.TryGetValue(item.Key, out var chartEntry) ? chartEntry : null;
            var sd = new ChartSerieData()
            {
                Y = data?.GetItemData(dataAlias).TryGetDecimal(cultureInfo) ?? 0
            };

            if (_xAxisFields.Count > 1 &&
                (ChartSettings.SeriesType == SeriesType.Bar || ChartSettings.SeriesType == SeriesType.Line) &&
                data != null)
            {
                sd.X = _xAxisFields
                    .Select(field => CreateAxisPart(data.GetMember(field.Alias), field.Property, cultureInfo).Label)
                    .ToArray();
            }
            else
            {
                sd.X = item.Label;
            }

            serie.Data.Add(sd);
        }

        ChartSettings.Series.Add(serie);
    }

    private List<AxisValueDescriptor> DistinctAxisValues(IEnumerable<RgfDynamicDictionary> chartData, IReadOnlyList<AxisFieldDescriptor> fields, CultureInfo cultureInfo)
    {
        var values = new List<AxisValueDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in chartData)
        {
            var axisValue = CreateAxisValue(row, fields, cultureInfo);
            if (seen.Add(axisValue.Key))
            {
                values.Add(axisValue);
            }
        }

        return values;
    }

    private AxisValueDescriptor CreateAxisValue(RgfDynamicDictionary row, IReadOnlyList<AxisFieldDescriptor> fields, CultureInfo cultureInfo)
    {
        var parts = fields
            .Select(field => CreateAxisPart(row.GetMember(field.Alias), field.Property, cultureInfo))
            .ToList();

        var key = string.Join("\u001f", parts.Select(part => part.KeyPart));
        var label = string.Join(" / ", parts.Select(part => part.Label));
        var axisValue = parts.Count == 1 ? parts[0].AxisValue : null;

        return new AxisValueDescriptor(key, label, axisValue);
    }

    private static AxisPartDescriptor CreateAxisPart(object? value, IRgfProperty? property, CultureInfo cultureInfo)
    {
        if (RgfDisplayValueFormatter.TryGetNormalizedDateTimeValue(value, property, out var normalizedDateTime))
        {
            var label = RgfDisplayValueFormatter.TryFormatDateDisplayValue(value, property, cultureInfo, out var formattedDateValue)
                ? formattedDateValue ?? string.Empty
                : normalizedDateTime.ToString("o", CultureInfo.InvariantCulture);

            return new AxisPartDescriptor(
                normalizedDateTime.Ticks.ToString(CultureInfo.InvariantCulture),
                label,
                normalizedDateTime);
        }

        var rawText = value?.ToString() ?? string.Empty;
        return new AxisPartDescriptor(rawText, rawText, null);
    }

    private sealed record AxisFieldDescriptor(string Alias, IRgfProperty? Property);

    private sealed record AxisPartDescriptor(string KeyPart, string Label, DateTime? AxisValue);

    private sealed record AxisValueDescriptor(string Key, string Label, DateTime? AxisValue);
}
