using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfChartParam
{
    public List<RgfGroupColumn> Columns { get; set; } = new List<RgfGroupColumn>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Take { get; set; }
}

public class RgfGroupColumn
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Aggregate { get; set; }

    public int PropertyId { get; set; } = 0;
}