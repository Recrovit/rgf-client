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
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded", "rgf-check-stylesheet-rgfcharts", "<div class=\"rgf-check-stylesheet-rgfcharts\" rgf-apexcharts=\"\">",
            GetBundleCssHref(), RgfApexChartsCssLib, RgfBlazorConfigurationExtension.GetBundleCssHref());

        await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_rgfApexChartsAssemblyName}/scripts/" +
#if DEBUG
            "recrovit-rgf-apexcharts.js"
#else
            "recrovit-rgf-apexcharts.min.js"
#endif
        );
    }

    public static async Task UnloadResourcesAsync(IJSRuntime jsRuntime)
    {
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{RgfApexChartsCssLib}')?.remove();");
        RgfBlazorConfiguration.UnregisterComponent(RgfBlazorConfiguration.ComponentType.Chart);
    }

    private static readonly string _rgfApexChartsAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

    private static readonly string RgfApexChartsCssLib = "rgf-apexcharts-lib";

    private static string GetBundleCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_rgfApexChartsAssemblyName}/{_rgfApexChartsAssemblyName}.bundle.scp.css?v={FileVersion}";

    public static readonly string JsApexChartsNamespace = "Recrovit.RGF.Blazor.ApexCharts";

    public static string FileVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version;
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
        var logger = serviceProvider.GetRequiredService<ILogger<RgfApexChartsConfiguration>>();
        logger?.LogInformation("RecroGrid Framework Blazor ApexCharts v{Version} initialized.", RgfApexChartsConfiguration.FileVersion);
    }
}