using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfSelectParam : EventArgs
{
    public string EntityName { get; set; }

    public RgfEntityKey ParentKey { get; set; }

    public int PropertyId { get; set; }

    public RgfEntityKey Filter { get; set; }

    public RgfEntityKey SelectedKey { get; set; }

    public EventDispatcher<CancelEventArgs> ItemSelectedEvent { get; } = new();
}
