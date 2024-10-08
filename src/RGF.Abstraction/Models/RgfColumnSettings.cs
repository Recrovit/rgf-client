using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfColumnSettings
{
    public RgfColumnSettings() { }

    public RgfColumnSettings(RgfProperty property)
    {
        Id = property.Id;
        Alias = property.Alias;
        ColPos = property.ColPos == 0 ? null : property.ColPos;
        ColWidth = property.ColWidth == 0 ? null : property.ColWidth;
        Sort = property.Sort == 0 ? null : property.Sort;
    }

    public int Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Alias { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColPos { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColWidth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sort { get; set; }
}