using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

[XmlRoot("columns")]
public class RgfFilter
{
    public enum LogicalOperator
    {
        And = 1,
        Or = 2
    }
    public enum QueryOperator
    {
        Invalid = 0,
        IsNull = 1,
        IsNotNull = 2,
        Equal = 3,
        NotEqual = 4,
        Like = 5,
        In = 6,
        NotIn = 7,
        Interval = 8,
        IntervalE = 9,
        Exists = 10,
        NotLike = 11
    }

    public static Dictionary<LogicalOperator, string> GetLogicalOperators(IRecroDictService recroDict)
    {
        Dictionary<LogicalOperator, string> dict = new()
        {
            { LogicalOperator.And, recroDict.GetRgfUiString("And") },
            { LogicalOperator.Or, recroDict.GetRgfUiString("Or") }
        };
        return dict;
    }
    public static Dictionary<QueryOperator, string> GetQueryOperators(QueryOperator[] queryOperator, IRecroDictService recroDict)
    {
        var dict = new Dictionary<QueryOperator, string>();
        if (queryOperator != null)
        {
            foreach (var op in queryOperator)
            {
                switch (op)
                {
                    case QueryOperator.IsNull:
                        dict.Add(op, "=Null");
                        break;

                    case QueryOperator.IsNotNull:
                        dict.Add(op, "<>Null");
                        break;

                    case QueryOperator.Equal:
                        dict.Add(op, "=");
                        break;

                    case QueryOperator.NotEqual:
                        dict.Add(op, "<>");
                        break;

                    case QueryOperator.Like:
                        dict.Add(op, recroDict.GetRgfUiString("Like"));
                        break;

                    case QueryOperator.NotLike:
                        dict.Add(op, recroDict.GetRgfUiString("NotLike"));
                        break;

                    case QueryOperator.In:
                        dict.Add(op, recroDict.GetRgfUiString("In"));
                        break;

                    case QueryOperator.NotIn:
                        dict.Add(op, recroDict.GetRgfUiString("NotIn"));
                        break;

                    case QueryOperator.Interval:
                        dict.Add(op, "<..<");
                        break;

                    case QueryOperator.IntervalE:
                        dict.Add(op, "<=..<=");
                        break;

                    case QueryOperator.Exists:
                        dict.Add(op, "Exists");
                        break;
                }
            }
        }
        return dict;
    }

    [XmlElement("column")]
    public List<Column> Columns { get; set; }

    public static bool Deserialize(string xmlString, out RgfFilter columns)
    {
        var serializer = new XmlSerializer(typeof(RgfFilter));
        using (var reader = new StringReader(xmlString))
        {
            columns = serializer.Deserialize(reader) as RgfFilter;
            if (columns != null)
            {
                return true;
            }
        }
        return false;
    }

    public class Column
    {
        [XmlAttribute("alias")]
        public string Alias { get; set; }

        [XmlAttribute("rgclass")]
        public string RgClass { get; set; }

        [XmlArray("dictionary")]
        [XmlArrayItem("item")]
        public List<DictionaryItem> Dictionary { get; set; }

        public QueryOperator[] Operators
        {
            get
            {
                var op = new QueryOperator[] { };
                if (RgClass.Contains("rg-select"))
                {
                    op = new QueryOperator[] { QueryOperator.In, QueryOperator.NotIn };
                }
                else if (RgClass.Contains("rg-checkbox"))
                {
                    op = new QueryOperator[] { QueryOperator.Equal, QueryOperator.NotEqual };
                }
                else if (RgClass.Contains("rg-numeric") || RgClass.Contains("datepicker") || RgClass.Contains("datetimepicker"))
                {
                    op = new QueryOperator[] { QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.Interval, QueryOperator.IntervalE };
                }
                else if (RgClass.Contains("rg-text"))
                {
                    op = new QueryOperator[] { QueryOperator.Like, QueryOperator.NotLike, QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.Interval, QueryOperator.IntervalE };
                }
                if (IsNullable)
                {
                    op = op.Concat(new QueryOperator[] { QueryOperator.IsNull, QueryOperator.IsNotNull }).ToArray();
                }
                return op;
            }
        }
        public bool IsNullable => RgClass.Contains("rg-nullable");
    }

    public class DictionaryItem
    {
        [XmlAttribute("id")]
        public string Key { get; set; }

        [XmlText]
        public string Value { get; set; }
    }

    [Serializable]
    public class Condition
    {
        public LogicalOperator LogicalOperator { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int PropertyId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public QueryOperator QueryOperator { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<Condition> Conditions { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Param1 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Param2 { get; set; }

        [JsonIgnore]
        public object Value1
        {
            get { return Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject() : Param1; }
            set { Param1 = value; }
        }

        [JsonIgnore]
        public object Value2
        {
            get { return Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject() : Param2; }
            set { Param2 = value; }
        }

        [JsonIgnore]
        public int ClientId { get; set; }

        #region Convert
        [JsonIgnore]
        public string StringValue1
        {
            get => Param1 == null ? null : Convert.ToString(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject(typeof(string)) : Param1);
            set { Param1 = value; }
        }
        [JsonIgnore]
        public string StringValue2
        {
            get => Param2 == null ? null : Convert.ToString(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject(typeof(string)) : Param2);
            set { Param2 = value; }
        }

        [JsonIgnore]
        public int? IntValue1
        {
            get => Param1 == null ? default(int?) : Convert.ToInt32(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject(typeof(int)) : Param1);
            set { Param1 = value; }
        }
        [JsonIgnore]
        public int? IntValue2
        {
            get => Param2 == null ? default(int?) : Convert.ToInt32(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject(typeof(int)) : Param2);
            set { Param2 = value; }
        }

        [JsonIgnore]
        public decimal? DecimalValue1
        {
            get => Param1 == null ? default(decimal?) : Convert.ToDecimal(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject(typeof(decimal)) : Param1);
            set { Param1 = value; }
        }
        [JsonIgnore]
        public decimal? DecimalValue2
        {
            get => Param2 == null ? default(decimal?) : Convert.ToDecimal(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject(typeof(decimal)) : Param2);
            set => Param2 = value;
        }

        [JsonIgnore]
        public double? DoubleValue1
        {
            get => Param1 == null ? default(double?) : Convert.ToDouble(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject(typeof(double)) : Param1);
            set { Param1 = value; }
        }
        [JsonIgnore]
        public double? DoubleValue2
        {
            get => Param2 == null ? default(double?) : Convert.ToDouble(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject(typeof(double)) : Param2);
            set => Param2 = value;
        }

        [JsonIgnore]
        public DateTime? DateTimeValue1
        {
            get => Param1 == null ? default(DateTime?) : Convert.ToDateTime(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject(typeof(DateTime)) : Param1);
            set { Param1 = value; }
        }
        [JsonIgnore]
        public DateTime? DateTimeValue2
        {
            get => Param2 == null ? default(DateTime?) : Convert.ToDateTime(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject(typeof(DateTime)) : Param2);
            set { Param2 = value; }
        }

        [JsonIgnore]
        public bool BooleanValue1
        {
            get
            {
                if (Param1 == null)
                {
                    Param1 = false;
                    return false;
                }
                return Convert.ToBoolean(Param1 is JsonElement ? ((JsonElement)Param1).ConvertToObject() : Param1);
            }
            set { Param1 = value; }
        }
        [JsonIgnore]
        public bool ToLower
        {
            get
            {
                if (Param2 == null)
                {
                    Param2 = false;
                    return false;
                }
                return Convert.ToBoolean(Param2 is JsonElement ? ((JsonElement)Param2).ConvertToObject() : Param2);
            }
            set { Param2 = value; }
        }

        [JsonIgnore]
        public string[] StringArray1
        {
            get
            {
                if (Param1 is string[])
                {
                    return (string[])Param1;
                }
                else if (Param1 is JsonElement)
                {
                    var list = ((JsonElement)Param1).ConvertToObject() as List<object>;
                    if (list != null)
                    {
                        return list.Select(e => e.ToString()).ToArray();
                        //return list.ToArray();
                    }
                }
                return new string[] { };
            }
            set { Param1 = value; }
        }
        #endregion
    }
}

