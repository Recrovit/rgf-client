using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.AI;

// RGF-DOC: rgf.client.recroby.integration
/// <summary>Identifies one client Grid instance by reference, retaining its label after the Grid is released.</summary>
public sealed class RgfRecrobyGridContext
{
    /// <summary>The last successfully initialized entity name.</summary>
    public string EntityName { get; private set; } = string.Empty;

    /// <summary>The last successfully initialized entity display name.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The current initialized manager, or null while unavailable. Does not own the manager.</summary>
    public IRgManager? Manager { get; private set; }

    /// <summary>Publishes a successfully initialized manager and its display data from the owning Grid.</summary>
    /// <param name="manager">The current manager whose initialization has completed.</param>
    public void SetManager(IRgManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        EntityName = manager.EntityDesc.EntityName;
        DisplayName = string.IsNullOrWhiteSpace(manager.EntityDesc.Title) ? EntityName : manager.EntityDesc.Title;
        Manager = manager;
    }

    /// <summary>Clears the manager before disposal only if it is still the expected instance; preserves display data.</summary>
    /// <param name="manager">The manager being released by the owning Grid.</param>
    public void ClearManager(IRgManager manager)
    {
        if (ReferenceEquals(Manager, manager)) Manager = null;
    }
}
