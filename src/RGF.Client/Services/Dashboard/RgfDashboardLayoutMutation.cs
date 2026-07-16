using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public static class RgfDashboardLayoutMutation
{
    public static bool TryResizeSplit(
        RgfDashboardDefinition? dashboard,
        string? parentPaneId,
        string? leadingPaneId,
        string? trailingPaneId,
        decimal leadingSize,
        decimal trailingSize,
        out string? errorMessage)
        => TryResizeSplit(
            dashboard,
            parentPaneId,
            leadingPaneId,
            trailingPaneId,
            leadingSize,
            trailingSize,
            out _,
            out errorMessage);

    public static bool TryResizeSplit(
        RgfDashboardDefinition? dashboard,
        string? parentPaneId,
        string? leadingPaneId,
        string? trailingPaneId,
        decimal leadingSize,
        decimal trailingSize,
        out string? failureCode,
        out string? errorMessage)
    {
        failureCode = null;
        errorMessage = null;

        if (dashboard == null)
        {
            failureCode = "dashboard-not-found";
            errorMessage = "Dashboard could not be resolved.";
            return false;
        }

        if (leadingSize <= 0 || trailingSize <= 0)
        {
            failureCode = "resize-split-size-invalid";
            errorMessage = "Split pane sizes must be positive.";
            return false;
        }

        var parentPane = FindPane(dashboard.Layout?.RootPane, parentPaneId);
        if (parentPane?.Split?.Panes == null)
        {
            failureCode = "resize-parent-pane-not-split";
            errorMessage = $"Parent pane '{parentPaneId}' could not be found or is not split.";
            return false;
        }

        var normalizedLeadingPaneId = NormalizePaneId(leadingPaneId);
        var normalizedTrailingPaneId = NormalizePaneId(trailingPaneId);
        var leadingIndex = parentPane.Split.Panes.FindIndex(splitPane => string.Equals(splitPane.Pane?.PaneId, normalizedLeadingPaneId, StringComparison.Ordinal));
        var trailingIndex = parentPane.Split.Panes.FindIndex(splitPane => string.Equals(splitPane.Pane?.PaneId, normalizedTrailingPaneId, StringComparison.Ordinal));

        if (leadingIndex < 0 || trailingIndex < 0)
        {
            failureCode = "resize-split-pane-not-found";
            errorMessage = "One or both split panes could not be found.";
            return false;
        }

        if (trailingIndex != leadingIndex + 1)
        {
            failureCode = "resize-split-pane-not-adjacent";
            errorMessage = "The requested split panes must be direct neighbors.";
            return false;
        }

        parentPane.Split.Panes[leadingIndex].Size = decimal.Round(leadingSize, 4);
        parentPane.Split.Panes[trailingIndex].Size = decimal.Round(trailingSize, 4);
        return true;
    }

    private static RgfDashboardPane? FindPane(RgfDashboardPane? rootPane, string? paneId)
    {
        var normalizedPaneId = NormalizePaneId(paneId);
        if (rootPane == null || normalizedPaneId == null)
        {
            return null;
        }

        return FindPaneRecursive(rootPane, normalizedPaneId);
    }

    private static RgfDashboardPane? FindPaneRecursive(RgfDashboardPane pane, string paneId)
    {
        if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
        {
            return pane;
        }

        var split = pane.Split;
        if (split?.Panes == null)
        {
            return null;
        }

        foreach (var splitPane in split.Panes)
        {
            var childPane = splitPane?.Pane;
            if (childPane == null)
            {
                continue;
            }

            var foundPane = FindPaneRecursive(childPane, paneId);
            if (foundPane != null)
            {
                return foundPane;
            }
        }

        return null;
    }

    private static string? NormalizePaneId(string? paneId)
        => string.IsNullOrWhiteSpace(paneId) ? null : paneId.Trim();
}
