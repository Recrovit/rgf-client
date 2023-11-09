using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

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
