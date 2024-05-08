using System;
using System.Collections.Generic;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class DictionaryExtension
{
    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, Func<TValue> factory)
    {
        if (!self.TryGetValue(key, out TValue val))
        {
            val = factory();
            self.Add(key, val);
        }
        return val;
    }

    public static string GetStringValue<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, string defaultValue = null)
    {
        if (self.TryGetValue(key, out var val))
        {
            return val.ToString();
        }
        return defaultValue;
    }

    public static int GetIntValue<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, int defaultValue = 0)
    {
        if (self.TryGetValue(key, out var val) &&
            int.TryParse(val.ToString(), out int res))
        {
            return res;
        }
        return defaultValue;
    }

    public static int? TryGetIntValue<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key)
    {
        if (self.TryGetValue(key, out var val) &&
            int.TryParse(val.ToString(), out int res))
        {
            return res;
        }
        return null;
    }

    public static long GetLongValue<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, long defaultValue = 0)
    {
        if (self.TryGetValue(key, out var val) &&
            long.TryParse(val.ToString(), out long res))
        {
            return res;
        }
        return defaultValue;
    }

    public static bool GetBoolValue<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key)
    {
        var value = self.GetStringValue(key);
        return value != null && (value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) || value == "1");
    }
}