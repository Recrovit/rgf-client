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
            Groups = source.Groups?.Select(g => new RgfIdAliasPair(g)).ToList();
            SubGroups = source.SubGroups?.Select(s => new RgfIdAliasPair(s)).ToList();
            Take = source.Take;
        }
    }

    public List<RgfAggregationColumn> Columns { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RgfIdAliasPair>? Groups { get; set; }

    [Obsolete("Use SubGroups property instead.", true)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public List<RgfIdAliasPair>? SubGroup { get => SubGroups; set => SubGroups = value; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RgfIdAliasPair>? SubGroups { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Take { get; set; }

    [JsonIgnore]
    public List<RgfIdAliasPair> GroupsOrEmpty => Groups ??= [];

    [JsonIgnore]
    public List<RgfIdAliasPair> SubGroupsOrEmpty => SubGroups ??= [];

    public void Normalize()
    {
        if (GroupsOrEmpty.Count ==  0) Groups = null;
        if (SubGroupsOrEmpty.Count == 0) SubGroups = null;  
        if (Take != null && Take <= 0) Take = null;
    }

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Sort { get; set; }

    public override object Clone() => DeepCopy(this)!;

    public static RgfAggregationColumn? DeepCopy(RgfAggregationColumn? source) => source == null ? null : new RgfAggregationColumn(source);
}
