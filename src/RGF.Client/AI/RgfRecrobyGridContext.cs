using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
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

    /// <summary>Whether the owning Grid has permanently closed.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Raised once when the owning Grid permanently closes.</summary>
    public event Action<RgfRecrobyGridContext>? Closed;

    /// <summary>Raised after the manager reference or retained display data changes.</summary>
    public event Action? Changed;

    /// <summary>Captures the current manager's session, entity and applied filter/sort without loading data.</summary>
    /// <returns>An independent allowlisted snapshot, or null while the manager is unavailable.</returns>
    public RgfGridRequest? CreateGridRequest()
    {
        var manager = Manager;
        if (manager is null) return null;
        var settings = manager.ListHandler.GetGridSettings();
        var applied = RgfGridSettings.DeepCopy(new RgfGridSettings
        {
            Conditions = settings.Conditions,
            Sort = settings.Sort
        })!;
        var created = manager.CreateGridRequest(request =>
        {
            request.EntityName = manager.EntityDesc.EntityName;
            request.ListParam = new RgfListParam { UserFilter = applied.Conditions, Sort = applied.Sort };
        });
        // Event handlers can replace the request or add unrelated Grid-operation data.
        var snapshot = RgfGridSettings.DeepCopy(new RgfGridSettings
        {
            Conditions = created.ListParam?.UserFilter,
            Sort = created.ListParam?.Sort
        })!;
        var result = RgfGridRequest.Create(new RgfSessionParams
        {
            SessionId = created.SessionId,
            GridId = created.GridId,
            Language = created.Language
        });
        result.EntityName = created.EntityName;
        result.ListParam = new RgfListParam { UserFilter = snapshot.Conditions, Sort = snapshot.Sort };
        return result;
    }

    /// <summary>Publishes a successfully initialized manager and its display data from the owning Grid.</summary>
    /// <param name="manager">The current manager whose initialization has completed.</param>
    public void SetManager(IRgManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (IsClosed) return;
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

    /// <summary>Permanently releases the manager and signals the end of this Grid's lifetime.</summary>
    public void Close()
    {
        if (IsClosed) return;
        IsClosed = true;
        var hadManager = Manager is not null;
        Manager = null;
        Closed?.Invoke(this);
        if (hadManager) Changed?.Invoke();
    }
}
