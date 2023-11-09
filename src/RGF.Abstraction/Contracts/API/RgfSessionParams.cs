using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfSessionParams
{
    public RgfSessionParams() { }

    public RgfSessionParams(RgfSessionParams param)
    {
        if (param != null)
        {
            SessionId = param.SessionId;
            GridId = param.GridId;
            Language = param.Language;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SessionId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string GridId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Language { get; set; }//TODO: küldeni
}
