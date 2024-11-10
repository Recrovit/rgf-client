namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts;

public class ChartSerie
{
    public string? Name { get; set; }

    public List<ChartSerieData> Data { get; set; } = [];
}

public class ChartSerieData
{
    public object? X { get; set; }

    public decimal Y { get; set; }
}