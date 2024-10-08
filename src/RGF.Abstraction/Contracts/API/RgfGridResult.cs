using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Collections.Generic;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfGridResult : RgfSessionParams
{
    public RgfEntity EntityDesc { get; set; }

    public object[][] Data { get; set; }

    public string[] DataColumns { get; set; }

    public int[] SelectedItems { get; set; }

    public Dictionary<string, object> Options { get; set; }

    public List<RgfGridSetting> GridSettingList { get; set; }
}