using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System.Collections.Concurrent;

namespace Recrovit.RecroGridFramework.Client.Services;

internal class RecroDictService : IRecroDictService, IRecroDictFormatter
{
    private const string StandardUiScope = "RGF.UI";
    private const string RecrobyUiScope = "RGF.Recroby.UI";

    private readonly ILogger _logger;
    private readonly IRgfApiService _apiService;
    private readonly SemaphoreSlim _initializationGate = new(1);
    private readonly Dictionary<string, string> _defaultLanguages;
    private volatile UiDictionaries? _ui;

    private sealed record UiDictionaries(string Language, Dictionary<string, string> Languages,
        ConcurrentDictionary<string, string> Standard, ConcurrentDictionary<string, string> Recroby);

    public RecroDictService(IConfiguration configuration, ILogger<RecroDictService> logger, IRgfApiService apiService)
    {
        _logger = logger;
        _apiService = apiService;
        var config = configuration.GetSection("Recrovit:RecroGridFramework:RecroDict");
        DefaultLanguage = (config.GetValue<string>("DefaultLanguage", "eng") ?? "eng").ToLower();
        _defaultLanguages = new() { [DefaultLanguage] = DefaultLanguage };
    }

    // RGF-DOC: rgf.client.recroby.integration
    public async Task InitializeAsync(string? language = null)
    {
        language = string.IsNullOrEmpty(language) ? DefaultLanguage : language;
        await _initializationGate.WaitAsync();
        try
        {
            if (string.Equals(language, _ui?.Language, StringComparison.OrdinalIgnoreCase)) return;

            var languages = await GetDictionaryAsync("RGF.Language", language, authClient: false);
            var standard = await GetDictionaryAsync(StandardUiScope, language, authClient: false);
            var recroby = await GetDictionaryAsync(RecrobyUiScope, language, authClient: false);
            _ui = new(language, languages.ToDictionary(), new(standard, standard.Comparer), new(recroby, recroby.Comparer));
            _logger.LogInformation("RecroDict:{Language} initialized.", language);
        }
        finally { _initializationGate.Release(); }
    }

    public string DefaultLanguage { get; }

    public Dictionary<string, string> Languages => _ui?.Languages ?? _defaultLanguages;

    public bool IsInitialized => _ui is not null;

    private static ConcurrentDictionary<string, MemoryCache> DictionaryCache { get; } = new();

    public virtual async Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string? language = null, bool authClient = true)
    {
        if (string.IsNullOrEmpty(language))
        {
            language = _ui?.Language ?? DefaultLanguage;
        }
        var dictCache = DictionaryCache.GetOrAdd(language, new MemoryCache(new MemoryCacheOptions()));
        var dict = await dictCache.GetOrCreateAsync(scope, async entry =>
        {
            var options = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
            entry.SetOptions(options);
            var res = await _apiService.GetAsync<Dictionary<string, string>>($"/rgf/api/RecroDict/{scope}/{language}", authClient: authClient);
            if (res.Success)
            {
                return new ConcurrentDictionary<string, string>(res.Result, new CaseInsensitiveStringComparer());
            }
            return new ConcurrentDictionary<string, string>();
        });
        return dict!;
    }

    public string GetRgfUiString(string resourceKey) => GetUiString(_ui?.Standard, StandardUiScope, resourceKey);

    public string GetRgfUiString(string resourceKey, params object[] args) => FormatResource(StandardUiScope, resourceKey, GetRgfUiString(resourceKey), args);

    public string GetRgfRecrobyString(string resourceKey) => GetUiString(_ui?.Recroby, RecrobyUiScope, resourceKey);

    public string GetRgfRecrobyString(string resourceKey, params object[] args) => FormatResource(RecrobyUiScope, resourceKey, GetRgfRecrobyString(resourceKey), args);

    private static string GetUiString(ConcurrentDictionary<string, string>? dictionary, string scope, string resourceKey) =>
        dictionary is null
            ? "RecroDict has not been initialized"
            : IRecroDictServiceExtension.GetItem(dictionary, resourceKey, $"{scope}.{resourceKey}");

    public string FormatResource(string? scope, string resourceKey, string value, params object[] args)
    {
        var formatResult = RecroDictFormatHelper.Format(value, args);
        if (!formatResult.ParameterCountMatches)
        {
            _logger.LogError("RecroDict formatting argument count mismatch. Scope:{Scope} ResourceKey:{ResourceKey} Expected:{ExpectedArgumentCount} Actual:{ActualArgumentCount}",
                scope,
                resourceKey,
                formatResult.ExpectedArgumentCount,
                formatResult.ActualArgumentCount);
        }

        if (!formatResult.Succeeded)
        {
            _logger.LogError(formatResult.Exception,
                "RecroDict formatting failed. Scope:{Scope} ResourceKey:{ResourceKey} Template:{Template}",
                scope,
                resourceKey,
                value);
        }

        return formatResult.Value;
    }
}

