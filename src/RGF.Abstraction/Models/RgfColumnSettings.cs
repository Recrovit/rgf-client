using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfColumnSettings : RgfIdAliasPair
{
    public RgfColumnSettings() { }

    public RgfColumnSettings(IRgfProperty property)
    {
        Id = property.Id;
        Alias = property.Alias;
        ColPos = property.ColPos == 0 ? null : property.ColPos;
        ColWidth = property.ColWidth == 0 ? null : property.ColWidth;
        Sort = property.Sort == 0 ? null : property.Sort;
    }

    internal RgfColumnSettings(RgfColumnSettings columnSettings) : base(columnSettings)
    {
        ColPos = columnSettings.ColPos;
        ColWidth = columnSettings.ColWidth;
        Sort = columnSettings.Sort;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColPos { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColWidth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sort { get; set; }

    public override object Clone() => DeepCopy(this);

    public static RgfColumnSettings DeepCopy(RgfColumnSettings source) => source == null ? null : new RgfColumnSettings(source);
}