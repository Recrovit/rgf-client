using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal sealed class FakeDashboardRecroDictService : IRecroDictService
{
    private static readonly string[] FallbackDashboardKeys =
    [
        "AssignSavedView",
        "CloneDashboard",
        "DashboardName",
        "DashboardNameFallback",
        "DashboardSection",
        "DashboardSizeTooltip",
        "Description",
        "DesignerPaneReadyToSplit",
        "DesignerPaneSelectToEdit",
        "DesignerPaneSplitHintSelected",
        "DesignerPaneSplitHintUnselected",
        "EditDashboard",
        "HeightPx",
        "LayoutSection",
        "LoadingDashboard",
        "LoadingDashboards",
        "MissingSavedViewWarning",
        "NewDashboard",
        "NoDashboardItem",
        "NoDashboardItems",
        "NoPanelSelected",
        "PanelHeaderVisible",
        "PanelSection",
        "PanelTitle",
        "RefreshSelectedPane",
        "RemoveSelectedPane",
        "SavedViewLeafOnly",
        "SavedViewSection",
        "Split",
        "SplitPaneColumns",
        "SplitPaneRows",
        "WidthPx"
    ];

    private static readonly string[] FallbackChartKeys =
    [
        "AdditionalGrouping",
        "Axis",
        "CreateChart",
        "DataSet",
        "GroupValues",
        "Legend",
        "Palette",
        "SelectDataColumns",
        "SeriesGrouping",
        "ShowDataLabels"
    ];

    private static readonly Regex DashboardKeyRegex = new("GetRgfUiDashboard\\(\"(?<key>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex ChartKeyRegex = new("GetRecroDictChart\\(\"(?<key>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Lazy<ConcurrentDictionary<string, string>> DashboardDictionary = new(CreateDashboardDictionary);
    private static readonly Lazy<ConcurrentDictionary<string, string>> ChartDictionary = new(CreateChartDictionary);

    public bool IsInitialized => true;

    public Dictionary<string, string> Languages { get; } = [];

    public string DefaultLanguage => "eng";

    public Task InitializeAsync(string language = null!) => Task.CompletedTask;

    public Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
        => Task.FromResult(CreateScopeDictionary(scope));

    public string GetRgfUiString(string resourceKey)
        => resourceKey;

    public string FormatResource(string scope, string resourceKey, string value, params object[] args)
        => args.Length == 0
            ? value
            : $"{value}({string.Join(", ", args.Select(arg => arg?.ToString() ?? string.Empty))})";

    public string GetRgfUiString(string resourceKey, params object[] args)
    {
        var value = GetRgfUiString(resourceKey);
        return FormatResource(scope: null!, resourceKey, value, args);
    }

    private static ConcurrentDictionary<string, string> CreateDashboardDictionary()
    {
        var repoRoot = ResolveRepositoryRoot();
        var keys = (repoRoot == null
                ? FallbackDashboardKeys
                : EnumerateKeys(repoRoot, EnumerateDashboardComponentFiles, DashboardKeyRegex))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => key, StringComparer.Ordinal);

        return new ConcurrentDictionary<string, string>(keys, StringComparer.Ordinal);
    }

    private static ConcurrentDictionary<string, string> CreateChartDictionary()
    {
        var repoRoot = ResolveRepositoryRoot();
        var keys = (repoRoot == null
                ? FallbackChartKeys
                : EnumerateKeys(repoRoot, EnumerateChartComponentFiles, ChartKeyRegex))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => key, StringComparer.Ordinal);

        return new ConcurrentDictionary<string, string>(keys, StringComparer.Ordinal);
    }

    private static ConcurrentDictionary<string, string> CreateScopeDictionary(string scope)
        => StringComparer.Ordinal.Equals(scope, "RGF.UI.Dashboard")
            ? new ConcurrentDictionary<string, string>(DashboardDictionary.Value, StringComparer.Ordinal)
            : StringComparer.Ordinal.Equals(scope, "RGF.UI.Chart")
                ? new ConcurrentDictionary<string, string>(ChartDictionary.Value, StringComparer.Ordinal)
                : new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

    private static IEnumerable<string> EnumerateKeys(
        string repoRoot,
        Func<string, IEnumerable<string>> fileEnumerator,
        Regex keyRegex)
    {
        foreach (var filePath in fileEnumerator(repoRoot))
        {
            var content = File.ReadAllText(filePath);
            foreach (Match match in keyRegex.Matches(content))
            {
                if (match.Groups["key"].Success)
                {
                    yield return match.Groups["key"].Value;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateDashboardComponentFiles(string repoRoot)
    {
        var dashboardUiPath = Path.Combine(repoRoot, "src", "RGF.Client.Blazor.UI", "Components", "Dashboard");
        if (Directory.Exists(dashboardUiPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(dashboardUiPath, "*.razor", SearchOption.AllDirectories))
            {
                yield return filePath;
            }
        }

        var dashboardBlazorPath = Path.Combine(repoRoot, "src", "RGF.Client.Blazor", "Components", "Dashboard");
        if (Directory.Exists(dashboardBlazorPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(dashboardBlazorPath, "*.razor", SearchOption.AllDirectories))
            {
                yield return filePath;
            }
        }
    }

    private static IEnumerable<string> EnumerateChartComponentFiles(string repoRoot)
    {
        var chartComponentPath = Path.Combine(repoRoot, "src", "RGF.Blazor.RgfApexCharts", "Components", "ChartComponent.razor");
        if (File.Exists(chartComponentPath))
        {
            yield return chartComponentPath;
        }
    }

    private static string? ResolveRepositoryRoot()
    {
        foreach (var startPath in GetCandidateRootPaths())
        {
            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "src"))
                    && Directory.Exists(Path.Combine(current.FullName, "tests")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidateRootPaths()
    {
        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
        {
            yield return Environment.CurrentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }

        var assemblyLocation = typeof(FakeDashboardRecroDictService).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                yield return assemblyDirectory;
            }
        }
    }
}
