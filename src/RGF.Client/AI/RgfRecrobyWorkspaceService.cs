using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.recroby.integration
/// <summary>Creates private ownership handles for external Workspace conversations.</summary>
public interface IRgfRecrobyWorkspaceService
{
    /// <summary>Creates an independent handle, bound when its first open reaches a usable Workspace.</summary>
    IRgfRecrobyOwner CreateOwner();
}

/// <summary>Owns external conversations; asynchronous disposal awaits their closure.</summary>
public interface IRgfRecrobyOwner : IAsyncDisposable
{
    /// <summary>Opens or activates an owner/type/context conversation and expands the Workspace.</summary>
    /// <param name="type">Nonblank application type; general is reserved (case insensitive).</param>
    /// <param name="contextKey">Nonblank client-side context, compared ordinally within this owner and type.</param>
    /// <param name="title">Initial trimmed title, at most 100 Unicode text elements.</param>
    /// <param name="sessionFactory">Called only for a new conversation on the Workspace dispatcher.</param>
    /// <returns>False if no unique usable Workspace exists, without calling the factory.</returns>
    /// <exception cref="ObjectDisposedException">The owner was disposed or its bound Workspace detached.</exception>
    /// <exception cref="ArgumentException">A key/title is blank, the type is reserved, or the title is too long.</exception>
    Task<bool> OpenOrActivateAsync(string type, string contextKey, string title, Func<RgfAiConversationSession> sessionFactory);
}

/// <summary>Scoped Workspace bridge. Stores ownership and registrations, never conversation sessions.</summary>
public sealed class RgfRecrobyWorkspaceService : IRgfRecrobyWorkspaceService
{
    private readonly object _sync = new();
    private readonly HashSet<Registration> _workspaces = [];
    private readonly HashSet<Owner> _owners = [];

    /// <inheritdoc />
    public IRgfRecrobyOwner CreateOwner() => new Owner(this);

    /// <summary>Registers a UI adapter; dispose the registration before releasing its UI state.
    /// Multiple simultaneous adapters reject opens until there is a unique target.</summary>
    /// <param name="dispatch">Awaits execution of an operation on the UI dispatcher.</param>
    /// <param name="open">Synchronous registration/activation; the opaque owner is compared by reference.</param>
    /// <param name="close">Awaits closing all conversations of the opaque owner on the UI dispatcher.</param>
    /// <returns>A deterministic detachment handle that invalidates owners bound to this adapter.</returns>
    public IDisposable RegisterWorkspace(Func<Func<bool>, Task<bool>> dispatch,
        Func<object, string, string, string, Func<RgfAiConversationSession>, bool> open,
        Func<object, Task> close)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(close);
        var registration = new Registration(this, dispatch, open, close);
        lock (_sync) _workspaces.Add(registration);
        return registration;
    }

    private sealed class Registration(RgfRecrobyWorkspaceService service,
        Func<Func<bool>, Task<bool>> dispatch,
        Func<object, string, string, string, Func<RgfAiConversationSession>, bool> open,
        Func<object, Task> close) : IDisposable
    {
        public Func<Func<bool>, Task<bool>> Dispatch { get; } = dispatch;
        public Func<object, string, string, string, Func<RgfAiConversationSession>, bool> Open { get; } = open;
        public Func<object, Task> Close { get; } = close;
        public void Dispose()
        {
            lock (service._sync)
            {
                service._workspaces.Remove(this);
                foreach (var owner in service._owners.Where(o => o.Workspace == this).ToArray())
                {
                    owner.Invalid = true;
                    service._owners.Remove(owner);
                }
            }
        }
    }

    private sealed class Owner(RgfRecrobyWorkspaceService service) : IRgfRecrobyOwner
    {
        public Registration? Workspace { get; private set; }
        public bool Invalid { get; set; }
        private Task? _disposal;

        public Task<bool> OpenOrActivateAsync(string type, string contextKey, string title, Func<RgfAiConversationSession> sessionFactory)
        {
            Registration workspace;
            lock (service._sync)
            {
                ObjectDisposedException.ThrowIf(Invalid, this);
                ArgumentException.ThrowIfNullOrWhiteSpace(type);
                ArgumentException.ThrowIfNullOrWhiteSpace(contextKey);
                ArgumentException.ThrowIfNullOrWhiteSpace(title);
                ArgumentNullException.ThrowIfNull(sessionFactory);
                type = type.Trim();
                title = title.Trim();
                if (type.Equals("general", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The general type is reserved for Workspace conversations.", nameof(type));
                if (StringInfo.ParseCombiningCharacters(title).Length > 100)
                    throw new ArgumentException("The title must contain at most 100 text elements.", nameof(title));
                if (service._workspaces.Count != 1) return Task.FromResult(false);
                workspace = service._workspaces.Single();
                if (Workspace is not null && Workspace != workspace) return Task.FromResult(false);
            }
            return workspace.Dispatch(() =>
            {
                lock (service._sync)
                {
                    ObjectDisposedException.ThrowIf(Invalid, this);
                    if (service._workspaces.Count != 1 || !service._workspaces.Contains(workspace)
                        || (Workspace is not null && Workspace != workspace)) return false;
                    Workspace = workspace;
                    service._owners.Add(this);
                    return workspace.Open(this, type, contextKey, title, () =>
                    {
                        var session = sessionFactory();
                        ObjectDisposedException.ThrowIf(Invalid, this);
                        return session;
                    });
                }
            });
        }

        public ValueTask DisposeAsync()
        {
            TaskCompletionSource completion;
            Registration? workspace;
            lock (service._sync)
            {
                if (_disposal is not null) return new(_disposal);
                Invalid = true;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = completion.Task;
                workspace = Workspace;
                service._owners.Remove(this);
            }
            return new(CompleteDisposalAsync(workspace, completion));
        }

        private async Task CompleteDisposalAsync(Registration? workspace, TaskCompletionSource completion)
        {
            try
            {
                if (workspace is not null) await workspace.Close(this);
                lock (service._sync) Workspace = null;
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
            await completion.Task;
        }
    }
}
