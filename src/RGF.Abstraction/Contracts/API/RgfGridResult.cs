using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfGridResult : RgfSessionParams
{
    public RgfEntity EntityDesc { get; set; }

    public object[][] Data { get; set; }

    public string[] DataColumns { get; set; }

    public int[] SelectedItems { get; set; }

    public Dictionary<string, object> Options { get; set; }
}
