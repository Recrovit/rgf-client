using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfFormResult
{
    public string XmlForm { get; set; }

    public Dictionary<string, object> InitParams { get; set; }

    public RgfEntityKey EntityKey { get; set; }

    public string CRUD { get; set; }

    public bool? DeletedPreviously { get; set; }

    public string StyleSheetUrl { get; set; }

    public RgfGridResult GridResult { get; set; }
}
