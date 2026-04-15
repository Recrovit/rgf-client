using System;
using System.Collections.Generic;
using System.Linq;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class StringExtensions
{
    public static string EnsureContains(this string originalString, string valuesToEnsure, char separatorChar, bool shouldTrim = true)
    {
        if (string.IsNullOrWhiteSpace(originalString))
        {
            return valuesToEnsure;
        }

        if (string.IsNullOrWhiteSpace(valuesToEnsure))
        {
            return originalString;
        }

        var delimiterArray = new[] { separatorChar };
        var requiredValues = valuesToEnsure.Split(delimiterArray, StringSplitOptions.RemoveEmptyEntries);
        var originalValues = originalString.Split(delimiterArray, StringSplitOptions.RemoveEmptyEntries).ToList();

        if (shouldTrim)
        {
            requiredValues = requiredValues.Select(value => value.Trim()).ToArray();
            originalValues = originalValues.Select(value => value.Trim()).ToList();
        }

        foreach (var value in requiredValues)
        {
            if (!originalValues.Contains(value))
            {
                originalValues.Add(value);
            }
        }

        return string.Join(separatorChar.ToString(), originalValues);
    }
}

public class CaseInsensitiveStringComparer : IEqualityComparer<string>
{
    public bool Equals(string x, string y)
    {
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(string obj)
    {
        return obj.ToLower().GetHashCode();
    }
}