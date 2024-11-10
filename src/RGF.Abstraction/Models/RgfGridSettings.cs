using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfGridSetting
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? GridSettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsPublic { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public bool IsPublicNonNullable { get => IsPublic ?? false; set { IsPublic = value; } }

    public bool IsReadonly { get; set; }
}

public class RgfGridSettings : RgfGridSetting
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfColumnSettings[] ColumnSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[][] Sort { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfFilter.Condition[] Filter { get; set; }

    public int PageSize { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SQLTimeout { get; set; }
}