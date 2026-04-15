using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;

public class EventRegistry<TEvent>
{
    private Dictionary<TEvent, EventDispatcher<EventArgs>> _dispatchers { get; } = new();

    public EventDispatcher<EventArgs> this[TEvent id] => GeDispatcher(id);

    public EventDispatcher<EventArgs> GeDispatcher(TEvent id) => _dispatchers.GetOrCreate(id, () => new EventDispatcher<EventArgs>());

    public EventDispatcher<EventArgs> TryGeDispatcher(TEvent id)
    {
        if (_dispatchers.TryGetValue(id, out var dispatcher))
        {
            return dispatcher;
        }
        return null;
    }

    public void Subscribe<TValue>(TEvent id, Func<TValue, Task> eventHandler) where TValue : EventArgs => GeDispatcher(id).Subscribe((Func<EventArgs, Task>)eventHandler);

    public void Subscribe<TValue>(TEvent id, Action<TValue> eventHandler) where TValue : EventArgs => GeDispatcher(id).Subscribe((Action<EventArgs>)eventHandler);

    public void Unsubscribe<TValue>(TEvent id, Func<TValue, Task> eventHandler) where TValue : EventArgs => GeDispatcher(id).Unsubscribe((Func<EventArgs, Task>)eventHandler);

    public void Unsubscribe<TValue>(TEvent id, Action<TValue> eventHandler) where TValue : EventArgs => GeDispatcher(id).Unsubscribe((Action<EventArgs>)eventHandler);

    public Task RaiseAsync<TValue>(TEvent id, TValue value) where TValue : EventArgs => TryGeDispatcher(id)?.InvokeAsync(value) ?? Task.CompletedTask;
}
