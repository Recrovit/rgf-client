using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class EnumExtension
{
    public static bool IsValid(this Enum enumerator)
    {
        bool defined = Enum.IsDefined(enumerator.GetType(), enumerator);
        if (!defined)
        {
            var attributes = (FlagsAttribute[])enumerator.GetType().GetCustomAttributes<FlagsAttribute>(false);

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

    public static string GetEnumMemberValue(this Enum enumerator)
    {
        var item = enumerator.GetType().GetMember(enumerator.ToString()).SingleOrDefault();
        return item?.GetCustomAttributes<EnumMemberAttribute>(false).FirstOrDefault()?.Value ?? enumerator.ToString();
    }

    public static TEnum GetEnumValueFromEnumMemberValue<TEnum>(string enumMemberValue, TEnum defaultValue) where TEnum : Enum
    {
        if (TryGetEnumValueFromEnumMemberValue(enumMemberValue, out TEnum value))
        {
            return value;
        }
        return defaultValue;
    }

    public static bool TryGetEnumValueFromEnumMemberValue<TEnum>(string enumMemberValue, out TEnum value) where TEnum : Enum
    {
        foreach (var field in typeof(TEnum).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(EnumMemberAttribute)) is EnumMemberAttribute attribute)
            {
                if (attribute.Value == enumMemberValue)
                {
                    value = (TEnum)field.GetValue(null);
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}