using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Models;

// RGF-DOC: rgf.client.ai.conversation-state

/// <summary>CustomFunction invocation settings and progress presentation owned by the list handler.</summary>
public class RgfCustomFunctionContext
{
    /// <summary>Backend CustomFunction name supplied by the application.</summary>
    public string? FunctionName { get; set; }

    /// <summary>Includes current list query parameters when true; defaults to false.</summary>
    public bool RequireQueryParams { get; set; }

    /// <summary>Application-defined custom parameters forwarded in the grid request.</summary>
    public Dictionary<string, object>? CustomParams { get; set; }

    /// <summary>Explicit signed target key; null permits existing selected-item behavior.</summary>
    public RgfEntityKey? EntityKey { get; set; }

    /// <summary>Optional toast displayed and updated by the progress infrastructure.</summary>
    public RgfToastEventArgs? Toast { get; set; }

    /// <summary>Enables progress setup when a synchronous callback or toast is also supplied; defaults to false.</summary>
    public bool EnableProgressTracking { get; set; }

    /// <summary>Synchronous progress callback; keep it short and leave connection disposal to the handler.</summary>
    public Action<IRgfProgressService, IRgfProgressArgs>? ProgressChanged;

    /// <summary>Optional asynchronous progress callback; by itself it does not activate progress setup.</summary>
    public Func<IRgfProgressService, IRgfProgressArgs, Task>? ProgressChangedAsync;
}