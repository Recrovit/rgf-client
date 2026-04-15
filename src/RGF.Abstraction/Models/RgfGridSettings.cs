using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfGridSetting : ICloneable
{
    public RgfGridSetting() { }

    internal RgfGridSetting(RgfGridSetting gridSetting)
    {
        if (gridSetting != null)
        {
            GridSettingsId = gridSetting.GridSettingsId;
            SettingsName = gridSetting.SettingsName;
            RoleId = gridSetting.RoleId;
            IsReadonly = gridSetting.IsReadonly;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? GridSettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReadonly { get; set; }

    public virtual object Clone() => DeepCopy(this);

    public static RgfGridSetting DeepCopy(RgfGridSetting source) => source == null ? null : new RgfGridSetting(source);
}

public class RgfGridSettings : RgfGridSetting
{
    public RgfGridSettings() { }

    internal RgfGridSettings(RgfGridSettings source) : base(source)
    {
        if (source != null)
        {
            ColumnSettings = source.ColumnSettings?.Select(x => new RgfColumnSettings(x)).ToArray();
            Sort = source.Sort?.Select(e => e.ToArray()).ToArray();
            if (source.Conditions?.Any() == true)
            {
                var conditionsJson = JsonSerializer.Serialize(source.Conditions, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                Conditions = JsonSerializer.Deserialize<RgfFilter.Condition[]>(conditionsJson);
            }
            PageSize = source.PageSize;
            SQLTimeout = source.SQLTimeout;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfColumnSettings[] ColumnSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[][] Sort { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfFilter.Condition[] Conditions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PageSize { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SQLTimeout { get; set; }

    public override object Clone() => DeepCopy(this);

    public static RgfGridSettings DeepCopy(RgfGridSettings source) => source == null ? null : new RgfGridSettings(source);
}