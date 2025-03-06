using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

[Obsolete("Use RgfCoreMessages instead", true)]
public class RgfMessages : RgfCoreMessages { }

public class RgfCoreMessages
{
    [JsonIgnore]
    public const string Default = "MessageDialog";

    [JsonIgnore]
    public const string MessageDialog = Default;

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

public static class RgfCoreMessagesExtensions
{
    public static void AddError(this RgfCoreMessages messages, string key, string value)
    {
        if (messages.Error == null)
        {
            messages.Error = new Dictionary<string, string>();
        }
        messages.Error[key] = value;
    }

    public static void AddWarning(this RgfCoreMessages messages, string key, string value)
    {
        if (messages.Warning == null)
        {
            messages.Warning = new Dictionary<string, string>();
        }
        messages.Warning[key] = value;
    }

    public static void AddInfo(this RgfCoreMessages messages, string key, string value)
    {
        if (messages.Info == null)
        {
            messages.Info = new Dictionary<string, string>();
        }
        messages.Info[key] = value;
    }

    public static void SetInfo(this RgfCoreMessages messages, string value) => messages.Info = new Dictionary<string, string> { { RgfCoreMessages.Default, value } };

    public static void SetWarning(this RgfCoreMessages messages, string value) => messages.Warning = new Dictionary<string, string> { { RgfCoreMessages.Default, value } };

    public static void SetError(this RgfCoreMessages messages, string value) => messages.Error = new Dictionary<string, string> { { RgfCoreMessages.Default, value } };
}