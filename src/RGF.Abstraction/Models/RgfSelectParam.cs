using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfSelectParam : EventArgs
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string EntityName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfEntityKey ParentKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PropertyId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfEntityKey Filter { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfEntityKey[] SelectedKeys { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public EventDispatcher<CancelEventArgs> ItemSelectedEvent { get; } = new();
}