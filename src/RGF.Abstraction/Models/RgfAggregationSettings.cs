using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfAggregationSettings : ICloneable
{
    public RgfAggregationSettings() { }

    internal RgfAggregationSettings(RgfAggregationSettings source)
    {
        Columns = source.Columns.Select(c => new RgfAggregationColumn(c)).ToList();
        Groups = source.Groups.Select(g => new RgfIdAliasPair(g)).ToList();
        SubGroup = source.SubGroup.Select(s => new RgfIdAliasPair(s)).ToList();
    }

    public List<RgfAggregationColumn> Columns { get; set; } = new();

    public List<RgfIdAliasPair> Groups { get; set; } = new();

    public List<RgfIdAliasPair> SubGroup { get; set; } = new();

    public virtual object Clone() => DeepCopy(this);

    public static RgfAggregationSettings DeepCopy(RgfAggregationSettings source) => source == null ? null : new RgfAggregationSettings(source);
}

public class RgfAggregationColumn : RgfIdAliasPair
{
    public static readonly string[] AllowedAggregates = { "Sum", "Avg", "Min", "Max", "Count", "-Sum" };

    public RgfAggregationColumn() { }

    internal RgfAggregationColumn(RgfAggregationColumn source) : base(source)
    {
        Aggregate = source.Aggregate;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Aggregate { get; set; }

    public override object Clone() => DeepCopy(this);

    public static RgfAggregationColumn DeepCopy(RgfAggregationColumn source) => source == null ? null : new RgfAggregationColumn(source);
}