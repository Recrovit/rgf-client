using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfChartDataRequest
{
    public RgfGridRequest GridRequest { get; set; }

    public RgfChartParam ChartParam { get; set; }
}