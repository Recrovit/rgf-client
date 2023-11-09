using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class ICollectionExtension
{
    public static void AddRange<T>(this ICollection<T> self, IEnumerable<T> source)
    {
        if (source != null)
        {
            foreach (var element in source)
            {
                self.Add(element);
            }
        }
    }
}