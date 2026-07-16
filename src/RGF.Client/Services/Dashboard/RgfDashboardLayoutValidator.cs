using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Models.Dashboard;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public enum RgfDashboardValidationMode
{
    Runtime = 0,
    Designer = 1
}

public static class RgfDashboardLayoutValidator
{
    public static RgfDashboardValidationResult Validate(
        RgfDashboardDefinition? dashboard,
        RgfDashboardValidationMode mode = RgfDashboardValidationMode.Runtime)
    {
        if (dashboard == null)
        {
            return new();
        }

        List<RgfDashboardValidationIssue> issues = [];
        var layout = dashboard.Layout ?? new();
        var itemIndex = RgfDashboardDefinitionHelper.BuildItemIndex(dashboard, issues);
        HashSet<int> usedDashboardItemIds = [];
        var items = layout.Items ?? [];

        if (layout.RootPane == null)
        {
            issues.Add(CreateIssue("root-missing", "Dashboard root pane is required."));
            return new(issues);
        }

        ValidatePane(
            layout.RootPane,
            itemIndex,
            new HashSet<string>(StringComparer.Ordinal),
            usedDashboardItemIds,
            new HashSet<RgfDashboardPane>(ReferenceEqualityComparer.Instance),
            new HashSet<RgfDashboardPane>(ReferenceEqualityComparer.Instance),
            issues);

        foreach (var item in items)
        {
            if (item == null || item.DashboardItemId == 0)
            {
                continue;
            }

            if (!itemIndex.ContainsKey(item.DashboardItemId))
            {
                continue;
            }

             if (item.ViewReference == null || string.IsNullOrWhiteSpace(item.ViewReference.EntityName))
            {
                issues.Add(CreateIssue(
                    "item-entity-missing",
                    $"Dashboard item '{GetItemName(item)}' (DashboardItemId: {item.DashboardItemId}) is missing its entity reference.",
                    dashboardItemId: item.DashboardItemId));
            }

            if (!issues.Any(issue => issue.Code == "unused-item" && issue.DashboardItemId == item.DashboardItemId)
                && !issues.Any(issue => issue.Code == "item-id-duplicate" && issue.DashboardItemId == item.DashboardItemId)
                && !usedDashboardItemIds.Contains(item.DashboardItemId))
            {
                issues.Add(CreateIssue(
                    "unused-item",
                    $"Dashboard item '{GetItemName(item)}' (DashboardItemId: {item.DashboardItemId}) is not referenced by any pane.",
                    dashboardItemId: item.DashboardItemId));
            }
        }

        return new(issues);
    }

    private static void ValidatePane(
        RgfDashboardPane? pane,
        IReadOnlyDictionary<int, RgfDashboardItem> itemIndex,
        HashSet<string> knownPaneIds,
        HashSet<int> usedDashboardItemIds,
        HashSet<RgfDashboardPane> activePath,
        HashSet<RgfDashboardPane> visitedPanes,
        List<RgfDashboardValidationIssue> issues)
    {
        if (pane == null)
        {
            issues.Add(CreateIssue("pane-missing", "Dashboard contains a missing pane."));
            return;
        }

        if (!activePath.Add(pane))
        {
            issues.Add(CreateIssue(
                "cycle-detected",
                $"Cycle detected while traversing pane '{pane.PaneId ?? "<null>"}'.",
                pane.PaneId));
            return;
        }

        if (!visitedPanes.Add(pane))
        {
            issues.Add(CreateIssue(
                "tree-structure-invalid",
                $"Pane '{pane.PaneId ?? "<null>"}' is referenced multiple times in the layout tree.",
                pane.PaneId));
            activePath.Remove(pane);
            return;
        }

        var paneId = string.IsNullOrWhiteSpace(pane.PaneId) ? null : pane.PaneId.Trim();
        if (string.IsNullOrEmpty(paneId))
        {
            issues.Add(CreateIssue("pane-id-missing", "Dashboard contains a pane without a PaneId."));
        }
        else if (!knownPaneIds.Add(paneId))
        {
            issues.Add(CreateIssue(
                "pane-id-duplicate",
                $"Pane id '{paneId}' is duplicated in the dashboard layout.",
                paneId));
        }

        var split = pane.Split;
        if (split == null)
        {
            ValidateLeafPane(pane, paneId, itemIndex, usedDashboardItemIds, issues);
            activePath.Remove(pane);
            return;
        }

        if (pane.DashboardItemId.HasValue)
        {
            issues.Add(CreateIssue(
                "split-item-conflict",
                $"Split pane '{paneId ?? "<null>"}' cannot reference DashboardItemId {pane.DashboardItemId.Value} directly.",
                paneId,
                pane.DashboardItemId));
        }

        if (split.Panes == null || split.Panes.Count < 2)
        {
            issues.Add(CreateIssue(
                "split-child-count",
                $"Split pane '{paneId ?? "<null>"}' must contain at least two child panes.",
                paneId));
        }

        if (split.Panes != null)
        {
            for (var i = 0; i < split.Panes.Count; i++)
            {
                var splitPane = split.Panes[i];
                if (splitPane == null)
                {
                    issues.Add(CreateIssue(
                        "split-pane-missing",
                        $"Split pane '{paneId ?? "<null>"}' contains a null child descriptor at index {i}.",
                        paneId));
                    continue;
                }

                if (splitPane.Size <= 0)
                {
                    issues.Add(CreateIssue(
                        "split-size-invalid",
                        $"Split pane '{paneId ?? "<null>"}' child at index {i} must have a positive Size.",
                        paneId));
                }

                if (splitPane.MinSize.HasValue && splitPane.MinSize.Value <= 0)
                {
                    issues.Add(CreateIssue(
                        "split-min-size-invalid",
                        $"Split pane '{paneId ?? "<null>"}' child at index {i} must have a positive MinSize when specified.",
                        paneId));
                }

                if (splitPane.Pane == null)
                {
                    issues.Add(CreateIssue(
                        "split-child-missing",
                        $"Split pane '{paneId ?? "<null>"}' child at index {i} is missing its pane.",
                        paneId));
                    continue;
                }

                ValidatePane(splitPane.Pane, itemIndex, knownPaneIds, usedDashboardItemIds, activePath, visitedPanes, issues);
            }
        }

        activePath.Remove(pane);
    }

    private static void ValidateLeafPane(
        RgfDashboardPane pane,
        string? paneId,
        IReadOnlyDictionary<int, RgfDashboardItem> itemIndex,
        HashSet<int> usedDashboardItemIds,
        List<RgfDashboardValidationIssue> issues)
    {
        if (!pane.DashboardItemId.HasValue)
        {
            return;
        }

        var dashboardItemId = pane.DashboardItemId.Value;
        if (!itemIndex.ContainsKey(dashboardItemId))
        {
            issues.Add(CreateIssue(
                "item-reference-missing",
                $"Pane '{paneId ?? "<null>"}' references missing DashboardItemId {dashboardItemId}.",
                paneId,
                dashboardItemId));
            return;
        }

        if (!usedDashboardItemIds.Add(dashboardItemId))
        {
            issues.Add(CreateIssue(
                "item-duplicate",
                $"Dashboard item {dashboardItemId} is referenced by multiple panes, including '{paneId ?? "<null>"}'.",
                paneId,
                dashboardItemId));
        }
    }

    private static RgfDashboardValidationIssue CreateIssue(
        string code,
        string message,
        string? paneId = null,
        int? dashboardItemId = null)
        => new()
        {
            Code = code,
            Message = message,
            PaneId = paneId,
            DashboardItemId = dashboardItemId
        };

    private static string GetItemName(RgfDashboardItem item, int index = -1)
        => RgfDashboardDefinitionHelper.GetDisplayTitle(item)
            ?? (item.DashboardItemId > 0 ? item.DashboardItemId.ToString() : null)
            ?? (index >= 0 ? $"#{index + 1}" : "unknown");
}
