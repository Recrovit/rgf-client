using System;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

/// <summary>Resolves notification managers by application-defined scope.</summary>
public interface IRgfEventNotificationService
{
    /// <summary>Gets or creates the notification manager for the supplied scope.</summary>
    IRgfNotificationManager GetNotificationManager(string scope);

    //void RaiseEvent<TArgs>(string scope, TArgs args, object sender);
    //IRgfObserver Subscribe<TArgs>(string scope, object receiver, Action<IRgfEventArgs<TArgs>> handler);
    /// <summary>Removes the scope registration; existing references and subscriptions remain separately owned.</summary>
    bool RemoveNotificationManager(string scope);
}

/// <summary>Owns typed event channels and subscriptions within a notification scope.</summary>
public interface IRgfNotificationManager : IDisposable
{
    /// <summary>Gets the event channel for the requested EventArgs type.</summary>
    IRgfObservableEvent<TArgs> GetObservableEvents<TArgs>() where TArgs : EventArgs;

    /// <summary>Publishes arguments together with the sender through the matching typed channel.</summary>
    Task RaiseEventAsync<TArgs>(TArgs args, object sender) where TArgs : EventArgs;

    /// <summary>Subscribes a synchronous handler; dispose the returned observer to unsubscribe.</summary>
    IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Action<IRgfEventArgs<TArgs>> handler) where TArgs : EventArgs;

    /// <summary>Subscribes an asynchronous handler; dispose the returned observer to unsubscribe.</summary>
    IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Func<IRgfEventArgs<TArgs>, Task> handler) where TArgs : EventArgs;
}

/// <summary>Typed channel for publishing notification envelopes.</summary>
public interface IRgfObservableEvent<TArgs> where TArgs : EventArgs
{
    /// <summary>Publishes one envelope through the channel.</summary>
    Task RaiseEventAsync(IRgfEventArgs<TArgs> args);
}

/// <summary>Disposable asynchronous notification observer.</summary>
public interface IRgfObserver<in TArgs> : IObserver<TArgs>, IDisposable
{
    /// <summary>Detaches this observer from its channel.</summary>
    void Unsubscribe();

    /// <summary>Processes one notification asynchronously.</summary>
    Task OnNextAsync(TArgs value);
}

/// <summary>Event source supporting the asynchronous RGF observer contract.</summary>
public interface IRgfObservable<out T> : IObservable<T>
{
    /// <summary>Attaches an observer until the returned subscription is disposed.</summary>
    IDisposable Subscribe(IRgfObserver<T> observer);
}

/// <summary>Notification metadata shared across typed event envelopes.</summary>
public interface IRgfEventArgs
{
    /// <summary>Object that originated the notification.</summary>
    object Sender { get; }

    /// <summary>Timestamp captured when the envelope was created.</summary>
    DateTime TriggeredAt { get; }

    /// <summary>Mutable signal indicating that a handler has processed the notification.</summary>
    bool Handled { get; set; }

    /// <summary>Mutable signal for consumers that support suppressing default behavior.</summary>
    bool PreventDefault { get; set; }
}

/// <summary>Typed notification arguments combined with sender and handling metadata.</summary>
public interface IRgfEventArgs<TEventArgs> : IRgfEventArgs where TEventArgs : EventArgs
{
    /// <summary>Original event payload.</summary>
    TEventArgs Args { get; }
}