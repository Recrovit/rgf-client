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

    /// <summary>Raised after the manager reference or retained display data changes.</summary>
    public event Action? Changed;

    /// <summary>Publishes a successfully initialized manager and its display data from the owning Grid.</summary>
    /// <param name="manager">The current manager whose initialization has completed.</param>
    public void SetManager(IRgManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var entityName = manager.EntityDesc.EntityName;
        var displayName = string.IsNullOrWhiteSpace(manager.EntityDesc.Title) ? entityName : manager.EntityDesc.Title;
        if (ReferenceEquals(Manager, manager) && EntityName == entityName && DisplayName == displayName) return;
        EntityName = entityName;
        DisplayName = displayName;
        Manager = manager;
        Changed?.Invoke();
    }

    /// <summary>Clears the manager before disposal only if it is still the expected instance; preserves display data.</summary>
    /// <param name="manager">The manager being released by the owning Grid.</param>
    public void ClearManager(IRgManager manager)
    {
        if (Manager is null || !ReferenceEquals(Manager, manager)) return;
        Manager = null;
        Changed?.Invoke();
    }
}
