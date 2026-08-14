using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.Blazor.Formatting;

public static class RgfDisplayValueFormatter
{
    public static bool TryFormatDisplayValue(object? value, IRgfProperty? property, CultureInfo culture, out string? formattedValue)
    {
        formattedValue = null;

        if (TryFormatDateDisplayValue(value, property, culture, out formattedValue))
        {
            return true;
        }

        return property != null
            ? TryFormatNumericDisplayValue(value, property, culture, out formattedValue)
            : TryFormatNumericDisplayValue(value, culture, out formattedValue);
    }

    public static bool ShouldFormatNumericDisplayValue(object? value, IRgfProperty? property)
        => property?.ListType == PropertyListType.Numeric
            && property.IsKey != true
            && value != null
            && value is not string
            && property.Options?.GetBoolValue("RGO_NoFormat") != true;

    public static bool TryFormatNumericDisplayValue(object? value, IRgfProperty? property, CultureInfo culture, out string? formattedValue)
    {
        formattedValue = null;

        if (!ShouldFormatNumericDisplayValue(value, property))
        {
            return false;
        }

        return TryFormatNumericDisplayValue(value, culture, out formattedValue);
    }

    public static bool TryFormatNumericDisplayValue(object? value, CultureInfo culture, out string? formattedValue, string format = "#,0.##")
    {
        formattedValue = null;

        if (!TryGetDecimalValue(value, out var decimalValue))
        {
            return false;
        }

        formattedValue = decimalValue.ToString(format, culture);
        return true;
    }

    public static bool IsDateDisplayProperty(IRgfProperty? property)
        => property?.ListType == PropertyListType.Date
            && property.FormType is PropertyFormType.Date or PropertyFormType.DateTime;

    public static bool TryFormatDateDisplayValue(object? value, IRgfProperty? property, CultureInfo culture, out string? formattedValue)
    {
        formattedValue = null;

        if (!TryGetNormalizedDateTimeValue(value, property, out var normalizedDateTime))
        {
            return false;
        }

        formattedValue = property!.FormType == PropertyFormType.DateTime
            ? string.Format("{0} {1}",
                normalizedDateTime.ToString("d", culture).Replace(" ", ""),
                normalizedDateTime.ToString("T", culture).Replace(" ", ""))
            : normalizedDateTime.ToString("d", culture).Replace(" ", "");

        return true;
    }

    public static bool TryGetNormalizedDateTimeValue(object? value, IRgfProperty? property, out DateTime normalizedDateTime)
    {
        normalizedDateTime = default;

        if (!IsDateDisplayProperty(property))
        {
            return false;
        }

        if (value is DateTime dateTime)
        {
            normalizedDateTime = property!.FormType == PropertyFormType.Date
                ? dateTime.Date
                : dateTime;
            return true;
        }

        if (value is DateOnly dateOnly)
        {
            normalizedDateTime = dateOnly.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        return false;
    }

    private static bool TryGetDecimalValue(object? value, out decimal decimalValue)
    {
        decimalValue = default;

        if (value == null)
        {
            return false;
        }

        try
        {
            var data = value is RgfDynamicData ? (RgfDynamicData)value : new RgfDynamicData(value);
            var number = data.TryGetDecimal();
            if (number != null)
            {
                decimalValue = number.Value;
                return true;
            }
        }
        catch { }
        return false;
    }
}
