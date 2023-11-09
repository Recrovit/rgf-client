using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRgfEventNotificationService
{
    IRgfNotificationManager GetNotificationManager(string scope);

    //void RaiseEvent<TArgs>(string scope, TArgs args, object sender);
    //IRgfObserver Subscribe<TArgs>(string scope, object receiver, Action<IRgfEventArgs<TArgs>> handler);
    bool RemoveNotificationManager(string scope);
}

public interface IRgfNotificationManager : IDisposable
{
    IRgfObservableEvent<TArgs> GetObservableEvents<TArgs>() where TArgs : EventArgs;

    void RaiseEvent<TArgs>(TArgs args, object sender) where TArgs : EventArgs;
    IRgfObserver Subscribe<TArgs>(object receiver, Action<IRgfEventArgs<TArgs>> handler) where TArgs : EventArgs;
    IRgfObserver Subscribe<TArgs>(object receiver, Func<IRgfEventArgs<TArgs>, Task> handler) where TArgs : EventArgs;
}

public interface IRgfObservableEvent<TArgs> where TArgs : EventArgs
{
    void RaiseEvent(IRgfEventArgs<TArgs> args);
}

public interface IRgfObserver : IDisposable
{
    void Unsubscribe();
}

public interface IRgfEventArgs<TArgs> where TArgs : EventArgs
{
    object Sender { get; }
    TArgs Args { get; }
}
