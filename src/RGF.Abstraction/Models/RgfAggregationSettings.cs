#nullable enable

using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfAggregationSettings : ICloneable
{
    public RgfAggregationSettings() { }

    internal RgfAggregationSettings(RgfAggregationSettings? source)
    {
        if (source != null)
        {
            Columns = source.Columns.Select(c => new RgfAggregationColumn(c)).ToList();
            Groups = source.Groups.Select(g => new RgfIdAliasPair(g)).ToList();
            SubGroup = source.SubGroup.Select(s => new RgfIdAliasPair(s)).ToList();
            Take = source.Take;
        }
    }

    public List<RgfAggregationColumn> Columns { get; set; } = new();

    public List<RgfIdAliasPair> Groups { get; set; } = new();

    public List<RgfIdAliasPair> SubGroup { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Take { get; set; }

    public virtual object Clone() => DeepCopy(this)!;

    public static RgfAggregationSettings? DeepCopy(RgfAggregationSettings? source) => source == null ? null : new RgfAggregationSettings(source);
}

public class RgfAggregationColumn : RgfIdAliasPair
{
    public static readonly string[] AllowedAggregates = { "Sum", "Avg", "Min", "Max", "Count", "-Sum" };

    public RgfAggregationColumn() { }

    internal RgfAggregationColumn(RgfAggregationColumn? source) : base(source)
    {
        if (source != null)
        {
            Aggregate = source.Aggregate;
            Sort = source.Sort;
        }
    }

    public string Aggregate { get; set; } = string.Empty;

    /// <summary>
    /// Aggregator-based API sort priority where <c>0</c> means unsorted, positive values mean ascending order,
    /// and negative values mean descending order; the absolute value stores the sort priority.
    /// </summary>
    public int Sort { get; set; }

    public override object Clone() => DeepCopy(this)!;

    public static RgfAggregationColumn? DeepCopy(RgfAggregationColumn? source) => source == null ? null : new RgfAggregationColumn(source);
}
