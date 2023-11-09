using System;
using System.Collections.Generic;
using System.Text;

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

    public static string GetStringValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, string defaultValue = null)
    {
        string res = defaultValue;
        if (self.TryGetValue(key, out var val))
        {
            res = val.ToString();
        }
        return res;
    }

    public static long GetLongValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, long defaultValue = 0)
    {
        long res = defaultValue;
        if (self.TryGetValue(key, out var val))
        {
            if (long.TryParse(val.ToString(), out long r))
            {
                res = r;
            }
        }
        return res;
    }

    public static bool GetBoolValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
    {
        var value = self.GetStringValue(key);
        return value != null && (value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) || value == "1");
    }
}
