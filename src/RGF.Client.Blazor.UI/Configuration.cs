using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Dashboard;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI;

// RGF-DOC: rgf.client.get-started.server-side.create-application
// RGF-DOC: rgf.client.get-started.wasm.create-application
// RGF-DOC: rgf.client.blazor-ui.entity-ui

/// <summary>Resource loading and version information for the standard Bootstrap-based RGF UI.</summary>
public class RGFClientBlazorUIConfiguration
{
    private static readonly string _rgfClientBlazorAssemblyName = typeof(RgfBlazorConfiguration).Assembly.GetName().Name!;
    private static readonly string _uiAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

    /// <summary>Loads UI scripts, backend resource references and styles; requires browser JS interop and prior base Blazor resource initialization.</summary>
    public static async Task LoadResourcesAsync(IServiceProvider serviceProvider, string? themeName = null)
    {
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();

        if (!string.IsNullOrEmpty(themeName))
        {
            ThemeName = themeName;
        }

        var jquiVer = await RgfBlazorConfiguration.ChkJQueryUiVer(jsRuntime);
        if (jquiVer < 0)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_rgfClientBlazorAssemblyName}/lib/jqueryui/jquery-ui.min.js");
        }

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RgfBlazorConfigurationExtension.GetJQueryUiCssHref(), false, JqueryUiCssId);

        if (!_scriptsLoaded)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap/dist/js/bootstrap.bundle.min.js");
        }
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetBootstrapCssHref(), false, BootstrapCssId);
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetBootstrapIconsCssHref(), false, BootstrapIconsId);
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetStylesCssHref(), false, BlazorUICss);

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded", "rgf-check-stylesheet-client-blazor-ui", "<div class=\"rgf-check-stylesheet-client-blazor-ui\" rgf-bs-root=\"\"></div>",
            GetBundleCssHref(), BlazorUICssLib, RgfBlazorConfigurationExtension.GetBundleCssHref());

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetBootstrapSubmenuCssHref(), false, BootstrapSubmenuCssId);

        if (!_scriptsLoaded)
        {
            var api = serviceProvider.GetRequiredService<IRgfApiService>();
            var res = await api.GetAsync<string[]>($"/rgf/api/RGFSriptReferences/-legacy-blazorui-", authClient: false);
            if (res.Success)
            {
                var sriptReferences = res.Result.Where(e => !RgfBlazorConfigurationExtension.SriptReferences.Contains(e)).ToArray();
                foreach (var item in sriptReferences)
                {
                    await jsRuntime.InvokeAsync<IJSObjectReference>("import", ApiService.BaseAddress + item);
                }
                await jsRuntime.InvokeVoidAsync("Recrovit.WebCli.InitRgfJqueryUiEx");
                _scriptsLoaded = true;
            }

            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/scripts/" +
#if DEBUG
                "recrovit-rgf-blazor-ui.js"
#else
                "recrovit-rgf-blazor-ui.min.js"
#endif
                );
        }

        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementsByTagName('html')[0].setAttribute('data-bs-theme', '{themeName}');");
    }

    /// <summary>Removes UI styles and unregisters shared UI/chart slots; does not unload executed JavaScript.</summary>
    public static async Task UnloadResourcesAsync(IJSRuntime jsRuntime)
    {
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{JqueryUiCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapIconsId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapSubmenuCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BlazorUICss}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BlazorUICssLib}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", "document.getElementsByTagName('html')[0].removeAttribute('data-bs-theme');");

        RgfBlazorConfiguration.UnregisterComponent(RgfBlazorConfiguration.ComponentType.Menu);
        RgfBlazorConfiguration.UnregisterComponent(RgfBlazorConfiguration.ComponentType.Dialog);
        RgfBlazorConfiguration.UnregisterComponent(RgfBlazorConfiguration.ComponentType.DashboardPage);
        RgfBlazorConfiguration.UnregisterComponent(RgfBlazorConfiguration.ComponentType.LoadingIndicator);

        await RgfApexChartsConfiguration.UnloadResourcesAsync(jsRuntime);
    }

    internal const string JqueryUiCssId = "rgf-jquery-ui";
    internal const string BootstrapCssId = "rgf-bootstrap";
    internal const string BootstrapIconsId = "rgf-bootstrap-icons";
    internal const string BlazorUICss = "rgf-client-blazor-ui";
    internal const string BlazorUICssLib = "rgf-client-blazor-ui-lib";
    internal const string BootstrapSubmenuCssId = "rgf-bootstrap-submenu";

    /// <summary>Returns the bundled Bootstrap stylesheet URL beneath AppRootPath.</summary>
    public static string GetBootstrapCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap/dist/css/bootstrap.min.css";

    /// <summary>Returns the bundled Bootstrap Icons stylesheet URL beneath AppRootPath.</summary>
    public static string GetBootstrapIconsCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap-icons/font/bootstrap-icons.min.css";

    internal static string GetStylesCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/css/styles.css";

    internal static string GetBundleCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/{_uiAssemblyName}.bundle.scp.css?v={Version}";

    internal static string GetBootstrapSubmenuCssHref() => $"{ApiService.BaseAddress}/rgf/resource/bootstrap-submenu.css";

    private static bool _scriptsLoaded;

    /// <summary>JavaScript namespace used by the standard UI interop helpers.</summary>
    public const string JsBlazorUiNamespace = "Recrovit.RGF.Blazor.UI";

    /// <summary>File version of the UI assembly.</summary>
    public static string Version => _version.Value;

    internal static string ThemeName { get; private set; } = "light";

    private static readonly Lazy<string> _version = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
}

/// <summary>Initializes standard UI component registrations and optional browser assets.</summary>
public static class RGFClientBlazorUIConfigurationExtension
{
    /// <summary>Registers standard menu, dialog, dashboard, loading, entity and chart implementations.</summary>
    /// <param name="serviceProvider">The built provider with RGF client and logging services.</param>
    /// <param name="themeName">Browser theme name; defaults to light.</param>
    /// <param name="loadResources">Whether to load browser assets. Pass false during SSR host startup.</param>
    /// <returns>A task completing after registration and optional UI/chart resource loading.</returns>
    /// <remarks>Call after Blazor initialization. Register application component overrides afterwards;
    /// this method replaces the shared UI slots and the fallback entity component.</remarks>
    public static async Task InitializeRgfUIAsync(this IServiceProvider serviceProvider, string themeName = "light", bool loadResources = true)
    {
        RgfBlazorConfiguration.RegisterComponent<MenuComponent>(RgfBlazorConfiguration.ComponentType.Menu);
        RgfBlazorConfiguration.RegisterComponent<DialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
        RgfBlazorConfiguration.RegisterComponent<DashboardPageComponent>(RgfBlazorConfiguration.ComponentType.DashboardPage);
        RgfBlazorConfiguration.RegisterComponent<SpinnerComponent>(RgfBlazorConfiguration.ComponentType.LoadingIndicator);
        RgfBlazorConfiguration.RegisterEntityComponent<EntityComponent>(string.Empty);

        if (loadResources)
        {
            await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider, themeName);
        }

        await serviceProvider.InitializeRGFBlazorApexChartsAsync(loadResources);
        RgfBlazorConfiguration.RegisterComponent<ChartComponent>(RgfBlazorConfiguration.ComponentType.Chart);

        var logger = serviceProvider.GetRequiredService<ILogger<RGFClientBlazorUIConfiguration>>();
        logger?.LogInformation("RecroGrid Framework Blazor.UI v{Version} initialized.", RGFClientBlazorUIConfiguration.Version);
    }

}
