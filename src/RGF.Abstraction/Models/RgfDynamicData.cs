using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System;
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

    public string Name { get; }

    private readonly object _lock = new object();
    private object _value;
    public virtual object Value
    {
        get => _value;
        set => SetValue(value);
    }

    private void SetValue(object value)
    {
        lock (_lock)
        {
            _value = value;

            OnAfterChange?.Invoke(this);

            var tasks = OnAfterChangeAsync?.GetInvocationList()
                .OfType<Func<RgfDynamicData, Task>>()
                .Select(callback => callback.Invoke(this));

            if (tasks != null)
            {
                _ = Task.WhenAll(tasks);
            }
        }
    }

    public string StringValue { get => Value?.ToString(); set => Value = value; }

    public short? ShortValue { get => Value as short?; set => Value = value; }

    public int? IntValue { get => (Value as int?) ?? ShortValue; set => Value = value; }

    public Int64? Int64Value { get => (Value as Int64?) ?? IntValue; set => Value = value; }

    public decimal? DecimalValue { get => (Value as decimal?) ?? Int64Value; set => Value = value; }

    public float? FloatValue { get => Value as float?; set => Value = value; }

    public double? DoubleValue { get => (Value as double?) ?? FloatValue; set => Value = value; }

    public DateTime? DateTimeValue { get => Value as DateTime?; set => Value = value; }

    public bool BooleanValue { get => Value as bool? ?? false; set => Value = value; }

    public event Action<RgfDynamicData> OnAfterChange;
    public event Func<RgfDynamicData, Task> OnAfterChangeAsync;

    public object ConvertToTypedValue(ClientDataType type, CultureInfo culture) => ConvertToTypedValue(type, Value, culture);
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

    public bool IsNumeric() => IsNumeric(Value);
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
                strValue = ((DateTime)Value).ToUniversalTime().ToString("O");
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
}