using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

[Obsolete("Use RgfCoreMessages instead", true)]
public class RgfMessages : RgfCoreMessages { }

public class RgfCoreMessages
{
    [JsonIgnore]
    public static string MessageDialog = "MessageDialog";

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string> Error { get; set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string> Warning { get; set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string> Info { get; set; }
}
