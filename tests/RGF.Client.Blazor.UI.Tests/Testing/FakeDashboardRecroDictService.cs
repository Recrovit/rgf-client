using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal sealed class FakeDashboardRecroDictService : IRecroDictService
{
    private static readonly Regex DashboardKeyRegex = new("GetRgfUiDashboard\\(\"(?<key>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Lazy<ConcurrentDictionary<string, string>> DashboardDictionary = new(CreateDashboardDictionary);

    public bool IsInitialized => true;

    public Dictionary<string, string> Languages { get; } = [];

    public string DefaultLanguage => "eng";

    public Task InitializeAsync(string language = null!) => Task.CompletedTask;

    public Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
        => Task.FromResult(
            string.Equals(scope, "RGF.UI.Dashboard", StringComparison.Ordinal)
                ? new ConcurrentDictionary<string, string>(DashboardDictionary.Value, StringComparer.Ordinal)
                : new ConcurrentDictionary<string, string>(StringComparer.Ordinal));

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
        var keys = EnumerateDashboardKeys(repoRoot)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => key, StringComparer.Ordinal);

        return new ConcurrentDictionary<string, string>(keys, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateDashboardKeys(string repoRoot)
    {
        foreach (var filePath in EnumerateDashboardComponentFiles(repoRoot))
        {
            var content = File.ReadAllText(filePath);
            foreach (Match match in DashboardKeyRegex.Matches(content))
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

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src"))
                && Directory.Exists(Path.Combine(current.FullName, "tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be resolved for FakeDashboardRecroDictService.");
    }
}
