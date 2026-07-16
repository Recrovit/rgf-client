using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Parameters;

public class RgfDashboardDesignerParameters : RgfSessionParams
{
    public RgfDashboardDesignerParameters() { }

    public RgfDashboardDesignerParameters(RgfSessionParams? sessionParams) : base(sessionParams) { }

    public RgfDashboardDefinition Dashboard { get; set; } = new();

    public IReadOnlyList<RgfDashboardEntityOption> EntityOptions { get; set; } = [];

    public EventCallback<RgfDashboardDefinition> DashboardEdited { get; set; }

    public EventCallback<bool> DirtyStateChanged { get; set; }

    public EventCallback SaveRequested { get; set; }

    public EventCallback DeleteRequested { get; set; }

    public bool CanSave { get; set; }

    public bool IsSaving { get; set; }

    public bool CanDelete { get; set; }

    public bool IsDeleting { get; set; }

    public bool ReadOnly { get; set; }

    public bool IsNameEditable { get; set; }

    public bool IsRoleSettingAllowed { get; set; }
}
