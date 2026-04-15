using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;

public class DataEventArgs<TValue> : EventArgs
{
    public TValue Value { get; private set; }

    public DataEventArgs(TValue value)
    {
        Value = value;
    }
}