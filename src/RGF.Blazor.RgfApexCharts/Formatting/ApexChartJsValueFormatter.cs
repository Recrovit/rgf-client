using System.Globalization;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Formatting;

internal static class ApexChartJsValueFormatter
{
    public static string CreateValueFormatter(CultureInfo cultureInfo)
    {
        var locale = EncodeJsString(cultureInfo.Name);
        return $"{RgfApexChartsConfiguration.JsApexChartsNamespace}.createValueFormatter('{locale}')";
    }

    public static string CreateTooltipYFormatter(CultureInfo cultureInfo)
    {
        var locale = EncodeJsString(cultureInfo.Name);
        return $"{RgfApexChartsConfiguration.JsApexChartsNamespace}.createTooltipYFormatter('{locale}')";
    }

    public static string CreateLegendFormatter(CultureInfo cultureInfo)
    {
        var locale = EncodeJsString(cultureInfo.Name);
        return $"{RgfApexChartsConfiguration.JsApexChartsNamespace}.createLegendFormatter('{locale}')";
    }

    private static string EncodeJsString(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
}
