using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfAggregationSettings : ICloneable
{
    public List<RgfAggregationColumn> Columns { get; set; } = new();

    public List<RgfIdAliasPair> Groups { get; set; } = new();

    public List<RgfIdAliasPair> SubGroup { get; set; } = new();

    public virtual object Clone()
    {
        var clone = new RgfAggregationSettings();
        clone.Columns = Columns.Select(c => c.Clone() as RgfAggregationColumn).ToList();
        clone.Groups = Groups.Select(g => g.Clone() as RgfIdAliasPair).ToList();
        clone.SubGroup = SubGroup.Select(s => s.Clone() as RgfIdAliasPair).ToList();
        return clone;
    }
}

public class RgfAggregationColumn : RgfIdAliasPair
{
    public static readonly string[] AllowedAggregates = { "Sum", "Avg", "Min", "Max", "Count", "-Sum" };

    public RgfAggregationColumn() { }

    public RgfAggregationColumn(RgfAggregationColumn aggregationColumn) : base(aggregationColumn) 
    {
        Aggregate = aggregationColumn.Aggregate;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Aggregate { get; set; }

    public override object Clone() => new RgfAggregationColumn(this);
}