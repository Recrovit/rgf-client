using System;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public enum RgfChartSeriesType
{
    Bar = 1,
    Line = 2,
    Pie = 3,
    Donut = 4
}

public class RgfChartSetting
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ChartSettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Remark { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsPublic { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public bool IsPublicNonNullable { get => IsPublic ?? false; set { IsPublic = value; } }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReadonly { get; set; }

    public RgfGridSettings ParentGridSettings { get; set; }
}

public class RgfChartSettings : RgfChartSetting, ICloneable
{
    public RgfChartSettings() { }

    public RgfAggregationSettings AggregationSettings { get; set; } = new();

    public RgfChartSeriesType SeriesType { get; set; } = RgfChartSeriesType.Bar;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; set; }

    public bool ShowDataLabels { get; set; }

    public bool Legend { get; set; }

    public bool Stacked { get; set; }

    public bool Horizontal { get; set; }

    public string Theme { get; set; }

    public string Palette { get; set; }

    public virtual object Clone()
    {
        var clone = (RgfChartSettings)MemberwiseClone();
        clone.AggregationSettings = (RgfAggregationSettings)this.AggregationSettings.Clone();
        return clone;
    }
}