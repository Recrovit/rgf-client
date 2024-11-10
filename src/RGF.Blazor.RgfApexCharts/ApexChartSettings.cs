using ApexCharts;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts;

public class ApexChartSettings
{
    public string? Title { get; set; }

    public ApexChartOptions<ChartSerieData> Options { get; set; } = new();

    public List<ChartSerie> Series { get; set; } = [];

    public SeriesType SeriesType { get; set; } = SeriesType.Bar;

    public bool ShowDataLabels { get; set; }

    public int? Height { get; set; }

    public int? Width { get; set; }
}