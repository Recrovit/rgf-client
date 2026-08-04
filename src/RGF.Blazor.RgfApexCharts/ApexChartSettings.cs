using ApexCharts;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class ApexChartSettings
{
    public ApexChartOptions<ChartSerieData> Options { get; set; } = new();

    public List<ChartSerie> Series { get; set; } = [];

    public SeriesType SeriesType { get; set; } = SeriesType.Bar;

    public RgfChartSeriesType ChartType { get; set; } = RgfChartSeriesType.Bar;

    public bool ShowDataLabels { get; set; }

    public int? Height { get; set; }

    public int? Width { get; set; }

    public RgfChartCardModel? Card { get; set; }
}
