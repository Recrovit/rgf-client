using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI;

public class RGFClientBlazorUIConfiguration
{
    public static async Task LoadResourcesAsync(IJSRuntime jsRuntime, string themeName)
    {
        //await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{ApiService.BaseAddress}/rgf/resource/lib%2Fjqueryui%2Fjquery-ui.min.js");
        //await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{ApiService.BaseAddress}/_content/recrogrid/lib/jqueryui/themes/base/jquery-ui.min.css", false, JqueryUiCssId);
        var bname = typeof(RgfBlazorConfiguration).Assembly.GetName().Name;
        await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{bname}/lib/jqueryui/jquery-ui.min.js");
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{bname}/lib/jqueryui/themes/base/jquery-ui.min.css", false, JqueryUiCssId);

        var libName = Assembly.GetExecutingAssembly().GetName().Name;
        await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/bootstrap/dist/js/bootstrap.bundle.min.js");
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/bootstrap/dist/css/bootstrap.min.css", false, BootstrapCssId);
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/bootstrap-icons/font/bootstrap-icons.min.css", false, BootstrapIconsId);
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/css/styles.css", false, BlazorUICss);
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/{libName}.bundle.scp.css", false, BlazorUICssLib);

        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{ApiService.BaseAddress}/rgf/resource/bootstrap-submenu.css", false, BootstrapSubmenuCssId);
        await jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.RemoveLinkedFile", "css/bootstrap/bootstrap.min.css", "stylesheet");

        await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/scripts/" +
#if DEBUG
            "recrovit-rgf-blazor-ui.js"
#else
            "recrovit-rgf-blazor-ui.min.js"
#endif
            );
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
    }

    private static readonly string JqueryUiCssId = "rgf-jquery-ui";
    private static readonly string BootstrapCssId = "rgf-bootstrap";
    private static readonly string BootstrapIconsId = "rgf-bootstrap-icons";
    private static readonly string BlazorUICss = "rgf-client-blazor-ui";
    private static readonly string BlazorUICssLib = "rgf-client-blazor-ui_lib";
    private static readonly string BootstrapSubmenuCssId = "rgf-bootstrap-submenu";

    public static readonly string JsBlazorUiNamespace = "Recrovit.RGF.Blazor.UI";
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
            var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
            await RGFClientBlazorUIConfiguration.LoadResourcesAsync(jsRuntime, themeName);
        }
        var ver = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var logger = serviceProvider.GetRequiredService<ILogger<RGFClientBlazorUIConfiguration>>();
        logger?.LogInformation($"RecroGrid Framework Blazor.UI v{ver} initialized.");
    }
}