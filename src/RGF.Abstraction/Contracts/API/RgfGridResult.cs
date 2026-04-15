using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfGridResult : RgfSessionParams
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfEntity EntityDesc { get; set; }

    public object[][] Data { get; set; }

    public string[] DataColumns { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[] SelectedItems { get; set; }

    public Dictionary<string, object> Options { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RgfGridSetting> GridSettingList { get; set; }
}