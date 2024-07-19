using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;
using Recrovit.RecroGridFramework.Client;
using Recrovit.RecroGridFramework.Client.Blazor;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts;

public class RgfApexChartsConfiguration
{
    public static async Task LoadResourcesAsync(IJSRuntime jsRuntime)
    {
        var libName = Assembly.GetExecutingAssembly().GetName().Name;
        //await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/css/styles.css", false, RgfApexCharts);
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/{libName}.bundle.scp.css", false, RgfApexChartsCssLib);
    }

    public static Task UnloadResourcesAsync(IJSRuntime jsRuntime)
    {
        //await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{RgfApexCharts}')?.remove();");
        return Task.CompletedTask;
    }

    //private static readonly string RgfApexCharts = "rgf-apexcharts";
    private static readonly string RgfApexChartsCssLib = "rgf-apexcharts-lib";
}

public static class RgfApexChartsConfigurationExtension
{
    public static async Task InitializeRGFBlazorApexChartsAsync(this IServiceProvider serviceProvider, bool loadResources = true)
    {
        RgfBlazorConfiguration.RegisterComponent<ChartComponent>(RgfBlazorConfiguration.ComponentType.Chart);

        if (loadResources)
        {
            var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
            await RgfApexChartsConfiguration.LoadResourcesAsync(jsRuntime);
        }
        var ver = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var logger = serviceProvider.GetRequiredService<ILogger<RgfApexChartsConfiguration>>();
        logger?.LogInformation($"RecroGrid Framework Blazor ApexCharts v{ver} initialized.");
    }
}