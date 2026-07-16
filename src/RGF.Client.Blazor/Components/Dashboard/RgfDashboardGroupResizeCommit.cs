namespace Recrovit.RecroGridFramework.Client.Blazor.Components.Dashboard;

public sealed class RgfDashboardGroupResizeCommit
{
    public int LeadingIndex { get; init; }

    public int TrailingIndex { get; init; }

    public decimal LeadingSize { get; init; }

    public decimal TrailingSize { get; init; }
}
