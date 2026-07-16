using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public sealed class RgfDashboardGroupResizeRequest
{
    public string? ParentPaneId { get; init; }

    public string LeadingPaneId { get; init; } = string.Empty;

    public string TrailingPaneId { get; init; } = string.Empty;

    public decimal LeadingSize { get; init; }

    public decimal TrailingSize { get; init; }
}
