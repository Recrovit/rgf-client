using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRecroDictService
{
    bool IsInitialized { get; }

    Task InitializeAsync(string language = null);

    Dictionary<string, string> Languages { get; }

    string DefaultLanguage { get; }

    Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null, bool authClient = true);

    string GetRgfUiString(string resourceKey);
}

public static class IRecroDictServiceExtension
{
    public static Task<string> GetTranslationAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language = null)
        => recroDict.GetItemAsync(scope, resourceKey, language, true);

    public static async Task<string> GetItemAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language = null, bool authClient = true)
    {
        var dictionary = await recroDict.GetDictionaryAsync(scope, language, authClient);
        return GetItem(dictionary, resourceKey, $"{scope}.{resourceKey}");
    }

    public static Task<string> GetItemAsync(this IRecroDictService recroDict, string scopedResourceKey, bool authClient = true, string language = null)
    {
        var lastIdx = scopedResourceKey.LastIndexOf('.');
        if (lastIdx > 0)
        {
            var scope = scopedResourceKey.Substring(0, lastIdx);
            var resourceKey = scopedResourceKey.Substring(lastIdx + 1);
            return GetItemAsync(recroDict, scope, resourceKey, language, authClient);
        }
        return GetItemAsync(recroDict, scopedResourceKey, "?", language, authClient);
    }

    public static string GetItem(this IRecroDictService recroDict, ConcurrentDictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => GetItem(dictionary, resourceKey, defaultValue);
    public static string GetItem(ConcurrentDictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => dictionary.TryGetValue(resourceKey, out var value) ? value : defaultValue;

    public static string GetItem(this IRecroDictService recroDict, Dictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => GetItem(dictionary, resourceKey, defaultValue);
    public static string GetItem(Dictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => dictionary.TryGetValue(resourceKey, out var value) ? value : defaultValue;
}