using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class EnumExtension
{
    public static bool IsValid(this Enum enumerator)
    {
        bool defined = Enum.IsDefined(enumerator.GetType(), enumerator);
        if (!defined)
        {
            FlagsAttribute[] attributes = (FlagsAttribute[])enumerator.GetType().GetCustomAttributes(typeof(FlagsAttribute), false);

            // If the value is a right bitwise match and
            // FlagsAttribute is uses, ToString returns 
            // all values separated with commas.
            if (attributes != null && attributes.Length > 0)
            {
                return enumerator.ToString().Contains(",");
            }
        }
        return defined;
    }
}
