namespace Recrovit.RecroGridFramework.Client.Models.Dashboard;

public sealed class RgfDashboardValidationResult
{
    public RgfDashboardValidationResult(IReadOnlyList<RgfDashboardValidationIssue>? issues = null)
    {
        Issues = issues ?? [];
    }

    public bool IsValid => Issues.Count == 0;

    public IReadOnlyList<RgfDashboardValidationIssue> Issues { get; }

    public string? FirstErrorMessage => Issues.FirstOrDefault()?.Message;
}

public sealed class RgfDashboardValidationIssue
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? PaneId { get; init; }

    public int? DashboardItemId { get; init; }
}
