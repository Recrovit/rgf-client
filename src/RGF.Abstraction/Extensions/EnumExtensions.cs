using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class EnumExtensions
{
    public static Dictionary<TEnum, string> ToDictionary<TEnum>() where TEnum : Enum
        => Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToDictionary(e => e, e => e.ToString());

    public static Dictionary<TEnum?, string> ToNullableDictionary<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToDictionary(e => (TEnum?)e, e => e.ToString());

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

    public static string GetEnumMemberValue(this Enum enumerator, string defaultValue = null) => enumerator.GetAttributeValue<EnumMemberAttribute>(attr => attr.Value, defaultValue) ?? enumerator.ToString();

    /// <summary>
    /// Retrieves the value of a custom attribute applied to an enum member, using a specified selector function.
    /// If the attribute is not found or the value is null, the provided default value is returned.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the custom attribute that should be applied to the enum member.</typeparam>
    /// <param name="enumerator">The enum member from which the attribute value will be retrieved.</param>
    /// <param name="valueSelector">A function that selects the value from the attribute.</param>
    /// <param name="defaultValue">The default value to return if the attribute is not found or if the selected value is null. If not provided, defaults to <c>null</c>.</param>
    /// <returns>The value from the attribute as selected by the <paramref name="valueSelector"/> function, or the <paramref name="defaultValue"/> if the attribute is not present or the value is null.</returns>
    /// <remarks>
    /// This method can be used to retrieve specific information from custom attributes applied to enum members.
    /// For example, if an enum member has a <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/>,
    /// the value can be retrieved using this method by passing a function that selects the <see cref="DisplayAttribute.Name"/> property:
    /// 
    /// <code>
    /// public enum Status
    /// {
    ///     [Display(Name = "Pending Approval")]
    ///     Pending,
    /// 
    ///     [Display(Name = "Approved")]
    ///     Approved,
    /// 
    ///     [Display(Name = "Rejected")]
    ///     Rejected
    /// }
    /// 
    /// // Example usage:
    /// var statusName = Status.Pending.GetAttributeValue<DisplayAttribute>(attr => attr.Name);
    /// </remarks>
    public static string GetAttributeValue<TAttribute>(this Enum enumerator, Func<TAttribute, string> valueSelector, string defaultValue = null)
        where TAttribute : Attribute
    {
        var item = enumerator.GetType().GetMember(enumerator.ToString()).SingleOrDefault();
        var attribute = item?.GetCustomAttributes<TAttribute>(false).FirstOrDefault();
        return (attribute != null ? valueSelector(attribute) : null) ?? defaultValue;
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