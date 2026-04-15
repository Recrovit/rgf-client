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

public class RgfChartSetting : ICloneable
{
    public RgfChartSetting() { }

    internal RgfChartSetting(RgfChartSetting chartSetting)
    {
        if (chartSetting != null)
        {
            ChartSettingsId = chartSetting.ChartSettingsId;
            SettingsName = chartSetting.SettingsName;
            RoleId = chartSetting.RoleId;
            IsReadonly = chartSetting.IsReadonly;
            if (chartSetting.ParentGridSettings != null)
            {
                ParentGridSettings = new RgfGridSettings(chartSetting.ParentGridSettings);
            }
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ChartSettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Remark { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReadonly { get; set; }

    public RgfGridSettings ParentGridSettings { get; set; }

    public virtual object Clone() => DeepCopy(this);

    public static RgfChartSetting DeepCopy(RgfChartSetting source) => source == null ? null : new RgfChartSetting(source);
}

public class RgfChartSettings : RgfChartSetting
{
    public RgfChartSettings() { }

    internal RgfChartSettings(RgfChartSettings chartSettings) : base(chartSettings)
    {
        if (chartSettings != null)
        {
            AggregationSettings = new RgfAggregationSettings(chartSettings.AggregationSettings);
            SeriesType = chartSettings.SeriesType;
            Height = chartSettings.Height;
            Width = chartSettings.Width;
            ShowDataLabels = chartSettings.ShowDataLabels;
            Legend = chartSettings.Legend;
            Stacked = chartSettings.Stacked;
            Horizontal = chartSettings.Horizontal;
            Theme = chartSettings.Theme;
            Palette = chartSettings.Palette;
        }
    }

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

    public override object Clone() => DeepCopy(this);

    public static RgfChartSettings DeepCopy(RgfChartSettings source) => source == null ? null : new RgfChartSettings(source);
}