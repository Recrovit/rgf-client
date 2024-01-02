using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Events;

public class ChangeEventArgs<TValue>
{
    public ChangeEventArgs(TValue oldValue, TValue newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public TValue OldValue { get; set; }

    public TValue NewValue { get; set; }

    public bool Cancel { get; set; }
}
