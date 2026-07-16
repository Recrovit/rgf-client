using System.Collections.Concurrent;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRecroDictService
{
    bool IsInitialized { get; }

    Task InitializeAsync(string language = null);

    Dictionary<string, string> Languages { get; }

    string DefaultLanguage { get; }

    Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null, bool authClient = true);

    string GetRgfUiString(string resourceKey);

    string GetRgfUiString(string resourceKey, params object[] args);
}

public interface IRecroDictFormatter
{
    string FormatResource(string scope, string resourceKey, string value, params object[] args);
}

public readonly record struct RecroDictFormatResult(string Value, int ExpectedArgumentCount, int ActualArgumentCount, Exception Exception)
{
    public bool ParameterCountMatches => ExpectedArgumentCount == ActualArgumentCount;

    public bool Succeeded => Exception == null;
}

public static class RecroDictFormatHelper
{
    public static RecroDictFormatResult Format(string value, params object[] args)
    {
        var expectedArgumentCount = CountExpectedArgumentCount(value);
        var actualArgumentCount = args?.Length ?? 0;
        var normalizedArguments = NormalizeArguments(expectedArgumentCount, args);

        try
        {
            return new(string.Format(value, normalizedArguments), expectedArgumentCount, actualArgumentCount, null);
        }
        catch (FormatException ex)
        {
            return new(value, expectedArgumentCount, actualArgumentCount, ex);
        }
    }

    private static object[] NormalizeArguments(int expectedArgumentCount, object[] args)
    {
        if (expectedArgumentCount == 0)
        {
            return [];
        }

        if (expectedArgumentCount == args.Length)
        {
            return args;
        }

        var normalizedArguments = new object[expectedArgumentCount];
        if (args == null || args.Length == 0)
        {
            return normalizedArguments;
        }

        Array.Copy(args, normalizedArguments, Math.Min(args.Length, expectedArgumentCount));
        return normalizedArguments;
    }

    private static int CountExpectedArgumentCount(string value)
    {
        int maxIndex = -1;

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '{')
            {
                if (i + 1 < value.Length && value[i + 1] == '{')
                {
                    i++;
                    continue;
                }

                int index = ParseIndex(value, i + 1);
                if (index >= 0)
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }
            else if (value[i] == '}' && i + 1 < value.Length && value[i + 1] == '}')
            {
                i++;
            }
        }

        return maxIndex + 1;
    }

    private static int ParseIndex(string value, int startIndex)
    {
        if (startIndex >= value.Length || !char.IsDigit(value[startIndex]))
        {
            return -1;
        }

        int index = 0;
        for (int i = startIndex; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return index;
            }

            index = (index * 10) + (value[i] - '0');
        }

        return index;
    }
}

public static class IRecroDictServiceExtension
{
    public static Task<string> GetTranslationAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language = null)
        => recroDict.GetItemAsync(scope, resourceKey, language, true);

    public static Task<string> GetTranslationAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language, params object[] args)
        => recroDict.GetItemAsync(scope, resourceKey, language, true, args);

    public static async Task<string> GetItemAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language = null, bool authClient = true)
    {
        var dictionary = await recroDict.GetDictionaryAsync(scope, language, authClient);
        return GetItem(dictionary, resourceKey, $"{scope}.{resourceKey}");
    }

    public static async Task<string> GetItemAsync(this IRecroDictService recroDict, string scope, string resourceKey, string language, bool authClient, params object[] args)
    {
        var dictionary = await recroDict.GetDictionaryAsync(scope, language, authClient);
        var value = GetItem(dictionary, resourceKey, $"{scope}.{resourceKey}");
        return FormatValue(recroDict, scope, resourceKey, value, args);
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

    public static Task<string> GetItemAsync(this IRecroDictService recroDict, string scopedResourceKey, bool authClient, string language, params object[] args)
    {
        var lastIdx = scopedResourceKey.LastIndexOf('.');
        if (lastIdx > 0)
        {
            var scope = scopedResourceKey.Substring(0, lastIdx);
            var resourceKey = scopedResourceKey.Substring(lastIdx + 1);
            return GetItemAsync(recroDict, scope, resourceKey, language, authClient, args);
        }

        return GetItemAsync(recroDict, scopedResourceKey, "?", language, authClient, args);
    }

    public static string GetItem(this IRecroDictService recroDict, ConcurrentDictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => GetItem(dictionary, resourceKey, defaultValue);
    public static string GetItem(ConcurrentDictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => dictionary.TryGetValue(resourceKey, out var value) ? value : defaultValue;

    public static string GetItem(this IRecroDictService recroDict, Dictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => GetItem(dictionary, resourceKey, defaultValue);
    public static string GetItem(Dictionary<string, string> dictionary, string resourceKey, string defaultValue = null) => dictionary.TryGetValue(resourceKey, out var value) ? value : defaultValue;

    public static string GetItem(this IRecroDictService recroDict, ConcurrentDictionary<string, string> dictionary, string resourceKey, string defaultValue, params object[] args)
        => FormatValue(recroDict, null, resourceKey, GetItem(dictionary, resourceKey, defaultValue), args);

    public static string GetItem(this IRecroDictService recroDict, Dictionary<string, string> dictionary, string resourceKey, string defaultValue, params object[] args)
        => FormatValue(recroDict, null, resourceKey, GetItem(dictionary, resourceKey, defaultValue), args);

    private static string FormatValue(IRecroDictService recroDict, string scope, string resourceKey, string value, params object[] args)
    {
        if (recroDict is IRecroDictFormatter formatter)
        {
            return formatter.FormatResource(scope, resourceKey, value, args);
        }

        return RecroDictFormatHelper.Format(value, args).Value;
    }
}
