using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfFilterSetting : ICloneable
{
    public RgfFilterSetting() { }

    internal RgfFilterSetting(RgfFilterSetting filterSetting)
    {
        if (filterSetting != null)
        {
            FilterSettingsId = filterSetting.FilterSettingsId;
            SettingsName = filterSetting.SettingsName;
            RoleId = filterSetting.RoleId;
            IsReadonly = filterSetting.IsReadonly;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FilterSettingsId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SettingsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoleId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReadonly { get; set; }

    public virtual object Clone() => DeepCopy(this);

    public static RgfFilterSetting DeepCopy(RgfFilterSetting source) => source == null ? null : new RgfFilterSetting(source);
}

public class RgfFilterSettings : RgfFilterSetting
{
    public RgfFilterSettings() { }

    internal RgfFilterSettings(RgfFilterSettings source) : base(source)
    {
        if (source != null)
        {
            if (source.Conditions?.Any() == true)
            {
                var conditionsJson = JsonSerializer.Serialize(source.Conditions, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                Conditions = JsonSerializer.Deserialize<RgfFilter.Condition[]>(conditionsJson);
            }
            SQLTimeout = source.SQLTimeout;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfFilter.Condition[] Conditions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SQLTimeout { get; set; }

    public override object Clone() => DeepCopy(this);

    public static RgfFilterSettings DeepCopy(RgfFilterSettings source) => source == null ? null : new RgfFilterSettings(source);
}