using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI;

public class RGFClientBlazorUIConfiguration
{
    private static readonly string _rgfClientBlazorAssemblyName = typeof(RgfBlazorConfiguration).Assembly.GetName().Name!;
    private static readonly string _uiAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

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

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RgfBlazorConfigurationExtension.GetJQueryUiCssHref(), false, RGFClientBlazorUIConfiguration.JqueryUiCssId);

        if (!_scriptsLoaded)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap/dist/js/bootstrap.bundle.min.js");
        }
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RGFClientBlazorUIConfiguration.GetBootstrapCssHref(), false, RGFClientBlazorUIConfiguration.BootstrapCssId);
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RGFClientBlazorUIConfiguration.GetBootstrapIconsCssHref(), false, RGFClientBlazorUIConfiguration.BootstrapIconsId);
        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RGFClientBlazorUIConfiguration.GetStylesCssHref(), false, RGFClientBlazorUIConfiguration.BlazorUICss);

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded", "rgf-check-stylesheet-client-blazor-ui", "<div class=\"rgf-check-stylesheet-client-blazor-ui\" rgf-bs-root=\"\"></div>",
            RGFClientBlazorUIConfiguration.GetBundleCssHref(), RGFClientBlazorUIConfiguration.BlazorUICssLib, RgfBlazorConfigurationExtension.GetBundleCssHref());

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", RGFClientBlazorUIConfiguration.GetBootstrapSubmenuCssHref(), false, RGFClientBlazorUIConfiguration.BootstrapSubmenuCssId);

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

    public static async Task UnloadResourcesAsync(IJSRuntime jsRuntime)
    {
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{JqueryUiCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapIconsId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BootstrapSubmenuCssId}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BlazorUICss}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", $"document.getElementById('{BlazorUICssLib}')?.remove();");
        await jsRuntime.InvokeVoidAsync("eval", "document.getElementsByTagName('html')[0].removeAttribute('data-bs-theme');");
        await RgfApexChartsConfiguration.UnloadResourcesAsync(jsRuntime);
    }

    internal const string JqueryUiCssId = "rgf-jquery-ui";
    internal const string BootstrapCssId = "rgf-bootstrap";
    internal const string BootstrapIconsId = "rgf-bootstrap-icons";
    internal const string BlazorUICss = "rgf-client-blazor-ui";
    internal const string BlazorUICssLib = "rgf-client-blazor-ui-lib";
    internal const string BootstrapSubmenuCssId = "rgf-bootstrap-submenu";

    internal static string GetBootstrapCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap/dist/css/bootstrap.min.css";

    internal static string GetBootstrapIconsCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/lib/bootstrap-icons/font/bootstrap-icons.min.css";

    internal static string GetStylesCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/css/styles.css";

    internal static string GetBundleCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_uiAssemblyName}/{_uiAssemblyName}.bundle.scp.css?v={Version}";

    internal static string GetBootstrapSubmenuCssHref() => $"{ApiService.BaseAddress}/rgf/resource/bootstrap-submenu.css";

    private static bool _scriptsLoaded;

    public const string JsBlazorUiNamespace = "Recrovit.RGF.Blazor.UI";

    public static string Version => _version.Value;

    internal static string ThemeName { get; private set; } = "light";

    private static readonly Lazy<string> _version = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
}

public static class RGFClientBlazorUIConfigurationExtension
{
    public static async Task InitializeRgfUIAsync(this IServiceProvider serviceProvider, string themeName = "light", bool loadResources = true)
    {
        RgfBlazorConfiguration.RegisterComponent<MenuComponent>(RgfBlazorConfiguration.ComponentType.Menu);
        RgfBlazorConfiguration.RegisterComponent<DialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
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
