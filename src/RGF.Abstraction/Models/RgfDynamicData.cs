using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfDynamicData : IEquatable<RgfDynamicData>
{
    public RgfDynamicData() { }

    public RgfDynamicData(object value) : this(null, value) { }

    public RgfDynamicData(ClientDataType type, object value, string name = null) : this(name, ConvertToTypedValue(type, value)) { }

    public RgfDynamicData(string name, object value) { Name = name; Value = value; }


    private readonly object _lock = new object();

    private object _value;

    public string Name { get; }

    public virtual object Value
    {
        get => _value;
        set => SetValue(value);
    }

    public event Action<RgfDynamicData> OnAfterChange;

    public event Func<RgfDynamicData, Task> OnAfterChangeAsync;


    private void SetValue(object value)
    {
        lock (_lock)
        {
            _value = value;

            OnAfterChange?.Invoke(this);

            var tasks = OnAfterChangeAsync?.GetInvocationList()
                .OfType<Func<RgfDynamicData, Task>>()
                .Select(handler => handler.Invoke(this))
                .ToArray();

            if (tasks?.Length > 0)
            {
                _ = Task.WhenAll(tasks);
            }
        }
    }

    public void SetValueSilently(object value)
    {
        lock (_lock)
        {
            _value = value;
        }
    }


    public object ObjectValue { get { return Value is JsonElement ? ((JsonElement)Value).ConvertToObject() : Value; } }

    public string StringValue
    {
        get
        {
            if (Value is string value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToString(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(string)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public short? ShortValue
    {
        get
        {
            if (Value is short value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToInt16(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(short)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public int? IntValue
    {
        get
        {
            if (Value is int value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToInt32(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(int)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public Int64? Int64Value
    {
        get
        {
            if (Value is Int64 value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToInt64(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(Int64)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public decimal? DecimalValue
    {
        get
        {
            if (Value is decimal value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToDecimal(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(decimal)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public float? FloatValue
    {
        get
        {
            if (Value is float value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToSingle(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(float)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public double? DoubleValue
    {
        get
        {
            if (Value is double value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToDouble(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(double)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public DateTime? DateTimeValue
    {
        get
        {
            if (Value is DateTime value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToDateTime(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(DateTime)) : Value);
                }
                catch { }
            }
            return null;
        }
        set => Value = value;
    }

    public string[] StringArray
    {
        get
        {
            if (Value is string[])
            {
                return (string[])Value;
            }
            if (Value is JsonElement)
            {
                var list = ((JsonElement)Value).ConvertToObject() as List<object>;
                if (list != null)
                {
                    return list.Select(e => e.ToString()).ToArray();
                }
            }
            return new string[] { };
        }
        set { Value = value; }
    }

    public bool BooleanValue
    {
        get
        {
            if (Value is bool value)
            {
                return value;
            }
            if (Value != null)
            {
                try
                {
                    return Convert.ToBoolean(Value is JsonElement ? ((JsonElement)Value).ConvertToObject(typeof(bool)) : Value);
                }
                catch { }
            }
            return false;
        }
        set => Value = value;
    }


    public static object ConvertToTypedValue(ClientDataType type, object data) => ConvertToTypedValue(type, data, new CultureInfo("en"));

    public static object ConvertToTypedValue(ClientDataType type, object data, CultureInfo culture)
    {
        object value;
        if (data == null)
        {
            value = data;
        }
        else if (data is JsonElement element)
        {
            switch (type)
            {
                case ClientDataType.String:
                    value = element.ConvertToObject(typeof(string));
                    break;

                //case DataType.Integer: int/long => auto select
                //break;

                case ClientDataType.Decimal:
                    value = element.ConvertToObject(typeof(decimal));
                    break;

                case ClientDataType.Double:
                    value = element.ConvertToObject(typeof(double));
                    break;

                case ClientDataType.DateTime:
                    value = element.ConvertToObject(typeof(DateTime));
                    break;

                case ClientDataType.Boolean:
                    value = element.ConvertToObject(typeof(bool));
                    break;

                default:
                    /*if (prop?.Alias == "__rgparams")
                    {
                        value = element.Deserialize<Dictionary<string, object>>();
                    }
                    else*/
                    {
                        value = element.ConvertToObject();
                    }
                    break;
            }
        }
        else
        {
            if (type != ClientDataType.String && data.Equals(string.Empty))
            {
                value = null;
            }
            else
            {
                switch (type)
                {
                    case ClientDataType.Undefined:
                        value = data;
                        break;

                    case ClientDataType.String:
                        value = Convert.ToString(data);
                        break;

                    case ClientDataType.Integer:
                        try { value = Convert.ToInt32(data, culture); }
                        catch { value = Convert.ToInt64(data, culture); }
                        break;

                    case ClientDataType.Decimal:
                        value = Convert.ToDecimal(data, culture);
                        break;

                    case ClientDataType.Double:
                        value = Convert.ToDouble(data, culture);
                        break;

                    case ClientDataType.DateTime:
                        value = Convert.ToDateTime(data, culture);
                        break;

                    case ClientDataType.Boolean:
                        value = Convert.ToBoolean(data, culture);
                        break;

                    default:
                        throw new NotImplementedException($"RgfDynamicData.ConvertToTypedValue => ClientDataType:{type}, data:{data}");
                }
            }
        }

        return value;
    }

    public static bool IsNumeric(Type type)
    {
        if (type != null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }
            TypeCode typeCode = Type.GetTypeCode(type);
            switch (typeCode)
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
            }
        }
        return false;
    }

    public static bool IsNumeric(object value) => value == null ? false : IsNumeric(value.GetType());

    public override string ToString() => ToString(new CultureInfo("en"));

    public string ToString(CultureInfo culture, string defaultValue = null)
    {
        string strValue = defaultValue;
        if (Value != null)
        {
            Type type = Value.GetType();
            if (type == typeof(string))
            {
                strValue = Value as string;
            }
            else if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                DateTime dateTimeValue = (DateTime)Value;
                if (dateTimeValue.TimeOfDay == TimeSpan.Zero)
                {
                    strValue = dateTimeValue.ToString("yyyy-MM-dd");
                }
                else
                {
                    if (dateTimeValue.Kind == DateTimeKind.Local)
                    {
                        dateTimeValue.ToUniversalTime();
                    }
                    strValue = dateTimeValue.ToString("O");
                }
            }
            else
            {
                strValue = TypeDescriptor.GetConverter(type).ConvertToString(null, culture, Value);
            }
        }
        return strValue;
    }

    public override int GetHashCode() => Value.ToString().GetHashCode();

    public override bool Equals(object other) => Equals(new RgfDynamicData(this.Name, other));

    public bool Equals(RgfDynamicData other)
    {
        var otherValue = other?.Value;
        bool valueIsJsonElement = Value is JsonElement;
        bool otherIsJsonElement = otherValue is JsonElement;
        bool valueIsNull = valueIsJsonElement ? ((JsonElement)Value).ValueKind == JsonValueKind.Null : Value == null;
        bool otherIsnull = otherIsJsonElement ? ((JsonElement)otherValue).ValueKind == JsonValueKind.Null : otherValue == null;
        if (!valueIsNull && !otherIsnull)
        {
            var type = Value.GetType();
            var otherType = otherValue.GetType();

            if (valueIsJsonElement && otherIsJsonElement)
            {
                string val1 = Value.ToString();
                string val2 = otherValue.ToString();
                return val1.Equals(val2);
            }

            if (type.Name == otherType.Name &&
                typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type))
            {
                return Value.Equals(otherValue);
            }

            var res = TryGetNumericEquality(this.Value, other.Value);
            if (res != null)
            {
                return res.Value;
            }

            bool primitive1 = type.IsPrimitive || Nullable.GetUnderlyingType(type)?.IsPrimitive == true;
            bool primitive2 = otherType.IsPrimitive || Nullable.GetUnderlyingType(otherType)?.IsPrimitive == true;

            if ((primitive1 || valueIsJsonElement) && (primitive2 || otherIsJsonElement))
            {
                string val1 = this.ToString();
                string val2 = other.ToString();
                var eq = val1.Equals(val2);
                return eq;
            }
            else
            {
#if DEBUG
                Console.WriteLine("RgfDynamicData.Equals => type1:{0}:{1}, type2:{2}:{3}", type.Name, this.ToString(), otherType.Name, other.ToString());
#endif
                Debug.WriteLine("RgfDynamicData.Equals => type1:{0}:{1}, type2:{2}:{3}", type.Name, this.ToString(), otherType.Name, other.ToString());
            }
        }
        return valueIsNull && otherIsnull;
    }

    public static bool? TryGetNumericEquality(object data1, object data2, CultureInfo culture = null)
    {
        if (data1 == null || data2 == null)
        {
            return null;
        }

        culture ??= CultureInfo.InvariantCulture;

        if (data1 is string strA)
        {
            if(!decimal.TryParse(strA, NumberStyles.Any, culture, out decimal dec))
            {
                return null;
            }
            data1 = dec;
        }
        
        if (data2 is string strB)
        {
            if (!decimal.TryParse(strB, NumberStyles.Any, culture, out decimal dec))
            {
                return null;
            }
            data2 = dec;
        }

        if (IsNumeric(data1) && IsNumeric(data2))
        {
            return Convert.ToDecimal(data1) == Convert.ToDecimal(data2);
        }

        return null;
    }
}

public static class RgfDynamicDataExtension
{
    public static decimal? TryGetDecimal(this RgfDynamicData data, IFormatProvider provider = null)
    {
        var val = data.DecimalValue;
        if (val == null)
        {
            if (decimal.TryParse(data.StringValue, NumberStyles.Any, provider, out decimal result))
            {
                val = result;
            }
        }
        return val;
    }

    public static object ConvertToTypedValue(this RgfDynamicData data, ClientDataType type, CultureInfo culture) => RgfDynamicData.ConvertToTypedValue(type, data.Value, culture);

    public static bool IsNumeric(this RgfDynamicData data) => RgfDynamicData.IsNumeric(data.Value);
}