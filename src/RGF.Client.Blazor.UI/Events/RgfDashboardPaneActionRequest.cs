using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Events;

public enum RgfDashboardPaneActionKind
{
    SplitColumns = 1,
    SplitRows = 2,
    AssignView = 3,
    ClearView = 4,
    RemovePane = 5,
    RefreshWidget = 6,
    RemoveSplit = 7
}

public sealed class RgfDashboardPaneActionRequest
{
    public string? PaneId { get; set; }

    public RgfDashboardPaneActionKind Action { get; set; }

    public RgfDashboardSplitDirection? SplitDirection { get; set; }

    public int? SplitCount { get; set; }
}
