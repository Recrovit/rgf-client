using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;

public class EventDispatcher<TValue>
{
    public EventDispatcher() { }

    public EventDispatcher(ILogger logger) { _logger = logger; }

    private readonly ILogger _logger;

    private event Action<TValue> _event;

    private event Func<TValue, Task> _eventAsync;

    public static EventDispatcher<TValue> operator +(EventDispatcher<TValue> eventHolder, Func<TValue, Task> eventHandler)
    {
        if (eventHandler != null)
        {
            eventHolder.Subscribe(eventHandler);
        }
        return eventHolder;
    }

    public static EventDispatcher<TValue> operator +(EventDispatcher<TValue> eventHolder, Action<TValue> eventHandler)
    {
        if (eventHandler != null)
        {
            eventHolder.Subscribe(eventHandler);
        }
        return eventHolder;
    }

    public static EventDispatcher<TValue> operator -(EventDispatcher<TValue> eventHolder, Func<TValue, Task> eventHandler)
    {
        if (eventHandler != null)
        {
            eventHolder.Unsubscribe(eventHandler);
        }
        return eventHolder;
    }

    public static EventDispatcher<TValue> operator -(EventDispatcher<TValue> eventHolder, Action<TValue> eventHandler)
    {
        if (eventHandler != null)
        {
            eventHolder.Unsubscribe(eventHandler);
        }
        return eventHolder;
    }

    public void Subscribe(Func<TValue, Task> eventHandler) => _eventAsync += eventHandler;

    public void Subscribe(Action<TValue> eventHandler) => _event += eventHandler;

    public void Unsubscribe(Func<TValue, Task> eventHandler) => _eventAsync -= eventHandler;

    public void Unsubscribe(Action<TValue> eventHandler) => _event -= eventHandler;

    public void Unsubscribe(object subscriberTarget)
    {
        if (_event != null)
        {
            foreach (var handler in _event.GetInvocationList())
            {
                if (handler.Target == subscriberTarget)
                {
                    _event -= (Action<TValue>)handler;
                }
            }
        }

        if (_eventAsync != null)
        {
            foreach (var handler in _eventAsync.GetInvocationList())
            {
                if (handler.Target == subscriberTarget)
                {
                    _eventAsync -= (Func<TValue, Task>)handler;
                }
            }
        }
    }

    public async Task InvokeAsync(TValue value)
    {
        var eventSync = _event;
        var eventAsync = _eventAsync;

        if (eventSync != null)
        {
            foreach (var handler in eventSync.GetInvocationList().OfType<Action<TValue>>())
            {
                _logger?.LogDebug("Invoke: {target}.{name}", handler.Target, handler.Method.Name);
                handler.Invoke(value);
            }
        }

        if (eventAsync != null)
        {
            var tasks = eventAsync.GetInvocationList()
                .OfType<Func<TValue, Task>>()
                .Select((handler) =>
                {
                    _logger?.LogDebug("Invoke: {target}.{name}", handler.Target, handler.Method.Name);
                    return handler.Invoke(value);
                })
                .ToArray();

            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
    }
}