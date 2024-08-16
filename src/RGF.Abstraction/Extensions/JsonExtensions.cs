using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public static class JsonElementExtensions
{
    public static object ConvertToObject(this JsonElement jsonElement) => jsonElement.ConvertToObject(null);

    public static object ConvertToObject(this JsonElement jsonElement, Type preferredType, string cultureName = "en")
    {
        if (preferredType != null)
        {
            var culture = new CultureInfo(cultureName);
            if (preferredType == typeof(DateTime)
                && DateTime.TryParse(jsonElement.ToString(), culture, DateTimeStyles.AllowWhiteSpaces, out DateTime dateTimeValue))
            {
                return dateTimeValue;
            }
            if (preferredType == typeof(decimal)
               && decimal.TryParse(jsonElement.ToString(), NumberStyles.Number, culture, out decimal decimalValue))
            {
                return decimalValue;
            }
            if (preferredType == typeof(double)
                && double.TryParse(jsonElement.ToString(), NumberStyles.Float, culture, out double doubleValue))
            {
                return doubleValue;
            }
            if (preferredType == typeof(float)
                && float.TryParse(jsonElement.ToString(), NumberStyles.Float, culture, out float floatValue))
            {
                return floatValue;
            }
            if (preferredType == typeof(short)
                && short.TryParse(jsonElement.ToString(), NumberStyles.Integer, culture, out short int16Value))
            {
                return int16Value;
            }
            if (preferredType == typeof(byte)
                && byte.TryParse(jsonElement.ToString(), NumberStyles.Integer, culture, out byte byteValue))
            {
                return byteValue;
            }
            if (preferredType == typeof(Guid)
                && jsonElement.TryGetGuid(out Guid guidValue))
            {
                return guidValue;
            }
            if (preferredType == typeof(string))
            {
                return jsonElement.ToString();
            }
        }
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                Dictionary<string, object> dictionary = new();
                foreach (var property in jsonElement.EnumerateObject())
                {
                    dictionary[property.Name] = property.Value.ConvertToObject();
                }
                return dictionary;

            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var element in jsonElement.EnumerateArray())
                {
                    list.Add(element.ConvertToObject());
                }
                return list;

            case JsonValueKind.String:
                return jsonElement.GetString();

            case JsonValueKind.Number:
                if (jsonElement.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                if (jsonElement.TryGetInt64(out long longValue))
                {
                    return longValue;
                }
                if (jsonElement.TryGetDecimal(out decimal decimalValue))
                {
                    return decimalValue;
                }
                if (jsonElement.TryGetDouble(out double doubleValue))
                {
                    return doubleValue;
                }
                break;

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            case JsonValueKind.Undefined:
                return jsonElement.ToString();
        }
        throw new NotImplementedException($"JsonElementExtensions.ConvertToObject => Value:{jsonElement.GetRawText()}, ValueKind:{jsonElement.ValueKind}");
    }
}