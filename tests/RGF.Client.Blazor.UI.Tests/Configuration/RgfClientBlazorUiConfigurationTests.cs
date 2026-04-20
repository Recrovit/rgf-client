using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.UI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Configuration;

public sealed class RgfClientBlazorUiConfigurationTests : IDisposable
{
    private const string ApiBaseAddress = "https://api.example.test";
    private const string AppRootPath = "/test-root";

    public RgfClientBlazorUiConfigurationTests()
    {
        RgfClientBlazorUiTestState.Reset();
        RgfClientBlazorUiTestState.ConfigureClientPaths(AppRootPath, ApiBaseAddress);
    }

    public void Dispose()
    {
        RgfClientBlazorUiTestState.Reset();
    }

    [Fact]
    public void Version_returns_current_assembly_file_version()
    {
        var expectedVersion = typeof(RGFClientBlazorUIConfiguration)
            .Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()!
            .Version;

        Assert.False(string.IsNullOrWhiteSpace(RGFClientBlazorUIConfiguration.Version));
        Assert.Equal(expectedVersion, RGFClientBlazorUIConfiguration.Version);
    }

    [Fact]
    public void JsBlazorUiNamespace_has_expected_value()
    {
        Assert.Equal("Recrovit.RGF.Blazor.UI", RGFClientBlazorUIConfiguration.JsBlazorUiNamespace);
    }

    [Fact]
    public async Task LoadResourcesAsync_loads_required_scripts_and_styles_on_first_run()
    {
        var jsRuntime = new RecordingJsRuntime { JQueryUiVersionComparisonResult = -1 };
        var apiService = new FakeRgfApiService
        {
            ScriptReferencesResult =
            [
                "/scripts/legacy-one.js",
                "/scripts/legacy-two.js",
            ],
        };

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService);

        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider, "dark");

        AssertImportOccurred(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor/lib/jqueryui/jquery-ui.min.js");
        AssertImportOccurred(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/lib/bootstrap/dist/js/bootstrap.bundle.min.js");
        AssertImportOccurred(jsRuntime, $"{ApiBaseAddress}/scripts/legacy-one.js");
        AssertImportOccurred(jsRuntime, $"{ApiBaseAddress}/scripts/legacy-two.js");
        AssertImportOccurred(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/scripts/{GetUiScriptFileName()}");

        Assert.Contains(apiService.Requests, request =>
            request.Uri == "/rgf/api/RGFSriptReferences/-legacy-blazorui-" &&
            request.AuthClient == false);

        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.AddStyleSheetLink"), invocation =>
            invocation.Arguments.Count >= 3 &&
            Equals(invocation.Arguments[0], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor/lib/jqueryui/themes/base/jquery-ui.min.css") &&
            Equals(invocation.Arguments[2], "rgf-jquery-ui"));
        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.AddStyleSheetLink"), invocation =>
            invocation.Arguments.Count >= 3 &&
            Equals(invocation.Arguments[0], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/lib/bootstrap/dist/css/bootstrap.min.css") &&
            Equals(invocation.Arguments[2], "rgf-bootstrap"));
        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.AddStyleSheetLink"), invocation =>
            invocation.Arguments.Count >= 3 &&
            Equals(invocation.Arguments[0], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/lib/bootstrap-icons/font/bootstrap-icons.min.css") &&
            Equals(invocation.Arguments[2], "rgf-bootstrap-icons"));
        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.AddStyleSheetLink"), invocation =>
            invocation.Arguments.Count >= 3 &&
            Equals(invocation.Arguments[0], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/css/styles.css") &&
            Equals(invocation.Arguments[2], "rgf-client-blazor-ui"));
        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.AddStyleSheetLink"), invocation =>
            invocation.Arguments.Count >= 3 &&
            Equals(invocation.Arguments[0], $"{ApiBaseAddress}/rgf/resource/bootstrap-submenu.css") &&
            Equals(invocation.Arguments[2], "rgf-bootstrap-submenu"));

        Assert.Contains(jsRuntime.GetInvocations("Recrovit.LPUtils.EnsureStyleSheetLoaded"), invocation =>
            invocation.Arguments.Count >= 5 &&
            Equals(invocation.Arguments[0], "rgf-check-stylesheet-client-blazor-ui") &&
            Equals(invocation.Arguments[2], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/Recrovit.RecroGridFramework.Client.Blazor.UI.bundle.scp.css?v={RGFClientBlazorUIConfiguration.Version}") &&
            Equals(invocation.Arguments[3], "rgf-client-blazor-ui-lib"));

        Assert.Contains(jsRuntime.GetInvocations("eval"), invocation =>
            invocation.Arguments.Count == 1 &&
            invocation.Arguments[0] is string script &&
            script.Contains("data-bs-theme', 'dark'", StringComparison.Ordinal));
        Assert.Contains(jsRuntime.GetInvocations("Recrovit.WebCli.InitRgfJqueryUiEx"), invocation => invocation.Arguments.Count == 0);
    }

    [Fact]
    public async Task LoadResourcesAsync_skips_jquery_ui_import_when_version_is_already_available()
    {
        var jsRuntime = new RecordingJsRuntime { JQueryUiVersionComparisonResult = 0 };
        var apiService = new FakeRgfApiService();

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService);

        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider);

        Assert.DoesNotContain(jsRuntime.GetInvocations("import"), invocation =>
            invocation.Arguments.Count == 1 &&
            Equals(invocation.Arguments[0], $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor/lib/jqueryui/jquery-ui.min.js"));
    }

    [Fact]
    public async Task LoadResourcesAsync_does_not_repeat_first_run_script_loading()
    {
        var jsRuntime = new RecordingJsRuntime { JQueryUiVersionComparisonResult = -1 };
        var apiService = new FakeRgfApiService
        {
            ScriptReferencesResult = ["/scripts/legacy-one.js"],
        };

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService);

        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider);
        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider);

        Assert.Equal(1, apiService.Requests.Count(request => request.Uri == "/rgf/api/RGFSriptReferences/-legacy-blazorui-"));
        Assert.Equal(1, CountImports(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/lib/bootstrap/dist/js/bootstrap.bundle.min.js"));
        Assert.Equal(1, CountImports(jsRuntime, $"{ApiBaseAddress}/scripts/legacy-one.js"));
        Assert.Equal(1, CountImports(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/scripts/{GetUiScriptFileName()}"));
        Assert.Single(jsRuntime.GetInvocations("Recrovit.WebCli.InitRgfJqueryUiEx"));
    }

    [Fact]
    public async Task LoadResourcesAsync_filters_out_already_registered_blazor_script_references()
    {
        var jsRuntime = new RecordingJsRuntime { JQueryUiVersionComparisonResult = -1 };
        var apiService = new FakeRgfApiService
        {
            ScriptReferencesResult =
            [
                "/scripts/existing.js",
                "/scripts/new-script.js",
            ],
        };

        RgfClientBlazorUiTestState.SetBlazorScriptReferences("/scripts/existing.js");

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService);

        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(serviceProvider);

        Assert.Equal(0, CountImports(jsRuntime, $"{ApiBaseAddress}/scripts/existing.js"));
        Assert.Equal(1, CountImports(jsRuntime, $"{ApiBaseAddress}/scripts/new-script.js"));
    }

    [Fact]
    public async Task UnloadResourcesAsync_removes_registered_styles_and_theme_attribute()
    {
        var jsRuntime = new RecordingJsRuntime();

        await RGFClientBlazorUIConfiguration.UnloadResourcesAsync(jsRuntime);

        var evalScripts = jsRuntime.GetInvocations("eval")
            .Select(invocation => invocation.Arguments.Single() as string)
            .Where(script => script != null)
            .ToArray();

        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-jquery-ui')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-bootstrap')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-bootstrap-icons')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-bootstrap-submenu')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-client-blazor-ui')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementById('rgf-client-blazor-ui-lib')?.remove();");
        Assert.Contains(evalScripts, script => script == "document.getElementsByTagName('html')[0].removeAttribute('data-bs-theme');");
    }

    [Fact]
    public async Task UnloadResourcesAsync_unregisters_chart_component_via_apexcharts_cleanup()
    {
        RgfBlazorConfiguration.RegisterComponent<ChartComponent>(RgfBlazorConfiguration.ComponentType.Chart);
        var jsRuntime = new RecordingJsRuntime();

        await RGFClientBlazorUIConfiguration.UnloadResourcesAsync(jsRuntime);

        Assert.False(RgfBlazorConfiguration.TryGetComponentType(RgfBlazorConfiguration.ComponentType.Chart, out _));
        Assert.Contains(jsRuntime.GetInvocations("eval"), invocation =>
            invocation.Arguments.Count == 1 &&
            Equals(invocation.Arguments[0], "document.getElementById('rgf-apexcharts-lib')?.remove();"));
    }

    [Fact]
    public async Task InitializeRgfUIAsync_registers_components_and_entity_component()
    {
        var jsRuntime = new RecordingJsRuntime();
        var apiService = new FakeRgfApiService();

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService);

        await serviceProvider.InitializeRgfUIAsync(loadResources: false);

        Assert.Equal(typeof(MenuComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu));
        Assert.Equal(typeof(DialogComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Dialog));
        Assert.Equal(typeof(ChartComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Chart));
        Assert.Equal(typeof(EntityComponent), RgfClientBlazorUiTestState.GetEntityComponentTypes()[string.Empty]);
    }

    [Fact]
    public async Task InitializeRgfUIAsync_with_loadResources_true_loads_ui_and_apexcharts_resources()
    {
        var jsRuntime = new RecordingJsRuntime();
        var apiService = new FakeRgfApiService();
        var loggerProvider = new ListLoggerProvider();

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService, loggerProvider);

        await serviceProvider.InitializeRgfUIAsync(themeName: "dark", loadResources: true);

        AssertImportOccurred(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Client.Blazor.UI/scripts/{GetUiScriptFileName()}");
        AssertImportOccurred(jsRuntime, $"{AppRootPath}/_content/Recrovit.RecroGridFramework.Blazor.RgfApexCharts/scripts/{GetApexChartsScriptFileName()}");

        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("RecroGrid Framework Blazor ApexCharts v", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("RecroGrid Framework Blazor.UI v", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitializeRgfUIAsync_with_loadResources_false_skips_resource_loading_but_still_registers_and_logs()
    {
        var jsRuntime = new RecordingJsRuntime();
        var apiService = new FakeRgfApiService();
        var loggerProvider = new ListLoggerProvider();

        using var serviceProvider = CreateServiceProvider(jsRuntime, apiService, loggerProvider);

        await serviceProvider.InitializeRgfUIAsync(loadResources: false);

        Assert.Empty(jsRuntime.Invocations);
        Assert.Empty(apiService.Requests);
        Assert.Equal(typeof(MenuComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu));
        Assert.Equal(typeof(DialogComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Dialog));
        Assert.Equal(typeof(ChartComponent), RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Chart));
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("RecroGrid Framework Blazor.UI v", StringComparison.Ordinal));
    }

    private static ServiceProvider CreateServiceProvider(
        RecordingJsRuntime jsRuntime,
        FakeRgfApiService apiService,
        ListLoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();

        if (loggerProvider == null)
        {
            services.AddLogging();
        }
        else
        {
            services.AddLogging(builder => builder.AddProvider(loggerProvider));
        }

        services.AddSingleton<IJSRuntime>(jsRuntime);
        services.AddSingleton<IRgfApiService>(apiService);

        return services.BuildServiceProvider();
    }

    private static void AssertImportOccurred(RecordingJsRuntime jsRuntime, string expectedImportPath)
    {
        Assert.Contains(jsRuntime.GetInvocations("import"), invocation =>
            invocation.Arguments.Count == 1 &&
            Equals(invocation.Arguments[0], expectedImportPath));
    }

    private static int CountImports(RecordingJsRuntime jsRuntime, string importPath)
        => jsRuntime.GetInvocations("import").Count(invocation =>
            invocation.Arguments.Count == 1 &&
            Equals(invocation.Arguments[0], importPath));

    private static string GetUiScriptFileName()
    {
#if DEBUG
        return "recrovit-rgf-blazor-ui.js";
#else
        return "recrovit-rgf-blazor-ui.min.js";
#endif
    }

    private static string GetApexChartsScriptFileName()
    {
#if DEBUG
        return "recrovit-rgf-apexcharts.js";
#else
        return "recrovit-rgf-apexcharts.min.js";
#endif
    }
}
