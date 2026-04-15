using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfGridRequest : RgfSessionParams
{
    [Obsolete("Use Manager CreateGridRequest instead")]
    public RgfGridRequest() { }

    [Obsolete("Use Manager CreateGridRequest instead", true)]
    public RgfGridRequest(RgfSessionParams param, string entityName, RgfListParam listParam = null) : base(param)
    {
        EntityName = entityName;
        ListParam = listParam;
    }

    private RgfGridRequest(RgfSessionParams param) : base(param) { }

    public static RgfGridRequest Create(RgfSessionParams param) => new RgfGridRequest(param);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string EntityName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Skeleton { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfListParam ListParam { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfDynamicDictionary Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfEntityKey EntityKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[] UserColumns { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfSelectParam SelectParam { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfGridSettings GridSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfChartSettings ChartSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfFilterSettings FilterSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object> CustomParams { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FunctionName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfGridRequest ParentGridRequest { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfFilterParent FilterParent { get; set; }
}