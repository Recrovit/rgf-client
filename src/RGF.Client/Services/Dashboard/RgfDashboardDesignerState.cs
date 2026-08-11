using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Models.Dashboard;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public sealed class RgfDashboardDesignerState
{
    private string? _baselineSnapshot;

    public RgfDashboardDefinition Dashboard { get; private set; } = new();

    public string? SelectedPaneId { get; private set; }

    public bool IsDirty { get; private set; }

    public bool IsNameEditable { get; private set; }

    public bool IsReadonly { get; private set; }

    public RgfDashboardValidationResult ValidationResult { get; private set; } = new();

    public void Load(RgfDashboardDefinition? dashboard, bool isNameEditable = false, bool isReadonly = false)
    {
        Dashboard = RgfDashboardDefinitionHelper.CreateNormalizedCopy(dashboard, pruneOrphanItems: true);

        ValidationResult = RgfDashboardLayoutValidator.Validate(Dashboard, RgfDashboardValidationMode.Designer);
        IsNameEditable = isNameEditable;
        IsReadonly = isReadonly || Dashboard.IsReadonly;
        SelectedPaneId = ResolveSelectedPaneId(Dashboard.Layout?.RootPane?.PaneId);
        _baselineSnapshot = Dashboard.SerializeSnapshot();
        IsDirty = false;
    }

    public RgfDashboardCommandResult SelectPane(string? paneId)
    {
        var normalizedPaneId = NormalizePaneId(paneId);
        if (normalizedPaneId == null || FindPaneLocation(Dashboard.Layout?.RootPane, normalizedPaneId) == null)
        {
            return RgfDashboardCommandResult.Failure(
                "pane-not-found",
                $"Pane '{paneId}' could not be found.",
                ValidationResult);
        }

        SelectedPaneId = normalizedPaneId;
        return RgfDashboardCommandResult.Success(ValidationResult);
    }

    public RgfDashboardCommandResult SetName(string? name)
        => ApplyMutation(clone =>
        {
            clone.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            return MutationOutcome.Success();
        });

    public RgfDashboardCommandResult SetDescription(string? description)
        => ApplyMutation(clone =>
        {
            clone.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            return MutationOutcome.Success();
        });

    public RgfDashboardCommandResult SetRoleId(string? roleId)
        => ApplyMutation(clone =>
        {
            clone.RoleId = string.IsNullOrWhiteSpace(roleId) ? null : roleId;
            return MutationOutcome.Success();
        });

    public RgfDashboardCommandResult SetWidth(decimal? width)
        => SetRootSize(width, Dashboard.Layout?.Height);

    public RgfDashboardCommandResult SetHeight(decimal? height)
        => SetRootSize(Dashboard.Layout?.Width, height);

    public RgfDashboardCommandResult SetRootSize(decimal? width, decimal? height)
        => ApplyMutation(clone =>
        {
            clone.Layout ??= new();
            clone.Layout.Width = width;
            clone.Layout.Height = height;
            return MutationOutcome.Success();
        });

    public RgfDashboardCommandResult SplitPane(string? paneId, RgfDashboardSplitDirection direction, int count)
        => ApplyMutation(clone =>
        {
            if (count < 2)
            {
                return MutationOutcome.Failure("split-count-invalid", "A split pane must create at least two child panes.");
            }

            var pane = FindPaneLocation(clone.Layout?.RootPane, paneId)?.Pane;
            if (pane == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (pane.Split != null)
            {
                return MutationOutcome.Failure("pane-not-leaf", $"Pane '{pane.PaneId}' is already split.");
            }

            var existingItemId = pane.DashboardItemId;
            pane.DashboardItemId = null;
            pane.Split = new()
            {
                Direction = direction,
                Panes = Enumerable.Range(0, count)
                    .Select(_ => new RgfDashboardSplitPane
                    {
                        Size = 1m,
                        Pane = new()
                    })
                    .ToList()
            };

            if (existingItemId is int dashboardItemId && pane.Split.Panes.Count > 0)
            {
                pane.Split.Panes[0].Pane.DashboardItemId = dashboardItemId;
            }

            return MutationOutcome.Success(pane.Split.Panes[0].Pane.PaneId);
        });

    public RgfDashboardCommandResult SetSplitPaneCount(string? paneId, int count)
        => ApplyMutation(clone =>
        {
            if (count < 2)
            {
                return MutationOutcome.Failure("split-count-invalid", "A split pane must contain at least two child panes.");
            }

            var pane = FindPaneLocation(clone.Layout?.RootPane, paneId)?.Pane;
            if (pane == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (pane.Split == null)
            {
                return MutationOutcome.Failure("pane-not-split", $"Pane '{pane.PaneId}' is not split.");
            }

            var currentCount = pane.Split.Panes.Count;
            if (count == currentCount)
            {
                return MutationOutcome.Success(pane.PaneId);
            }

            if (count > currentCount)
            {
                pane.Split.Panes.AddRange(Enumerable.Range(0, count - currentCount)
                    .Select(_ => new RgfDashboardSplitPane
                    {
                        Size = 1m,
                        Pane = new()
                    }));
            }
            else
            {
                pane.Split.Panes.RemoveRange(count, currentCount - count);
            }

            return MutationOutcome.Success(pane.PaneId);
        });

    public RgfDashboardCommandResult AssignItem(string? paneId, RgfDashboardViewReference? viewReference)
        => ApplyMutation(clone =>
        {
            var pane = FindPaneLocation(clone.Layout?.RootPane, paneId)?.Pane;
            if (pane == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (pane.Split != null)
            {
                return MutationOutcome.Failure("pane-not-leaf", $"Pane '{pane.PaneId}' is not a leaf pane.");
            }

            var resolvedViewReference = viewReference.DeepCopy() ?? new();
            resolvedViewReference.EntityName = resolvedViewReference.EntityName?.Trim() ?? string.Empty;
            resolvedViewReference.SettingsName = resolvedViewReference.SettingsName?.Trim() ?? string.Empty;

            var layout = EnsureLayout(clone);
            if (pane.DashboardItemId is int existingItemId)
            {
                var existingItem = layout.Items.FirstOrDefault(item => item.DashboardItemId == existingItemId);
                if (existingItem == null)
                {
                    return MutationOutcome.Failure("item-not-found", $"Pane '{pane.PaneId}' references a missing dashboard item.");
                }

                existingItem.ViewReference = resolvedViewReference;
                return MutationOutcome.Success(pane.PaneId);
            }

            var newItemId = GetNextDashboardItemId(clone);
            layout.Items.Add(new()
            {
                DashboardItemId = newItemId,
                ViewReference = resolvedViewReference
            });
            pane.DashboardItemId = newItemId;

            return MutationOutcome.Success(pane.PaneId);
        });

    public RgfDashboardCommandResult SetPaneHeaderVisibility(string? paneId, bool showHeader)
        => ApplyMutation(clone =>
        {
            var pane = FindPaneLocation(clone.Layout?.RootPane, paneId)?.Pane;
            if (pane == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (pane.Split != null)
            {
                return MutationOutcome.Failure("pane-not-leaf", $"Pane '{pane.PaneId}' is not a leaf pane.");
            }

            if (pane.DashboardItemId is not int dashboardItemId)
            {
                return MutationOutcome.Failure("pane-item-missing", $"Pane '{pane.PaneId}' does not reference a dashboard item.");
            }

            var item = EnsureLayout(clone).Items.FirstOrDefault(candidate => candidate.DashboardItemId == dashboardItemId);
            if (item == null)
            {
                return MutationOutcome.Failure("item-not-found", $"Pane '{pane.PaneId}' references a missing dashboard item.");
            }

            item.ShowHeader = showHeader;
            return MutationOutcome.Success(pane.PaneId);
        });

    public RgfDashboardCommandResult SetPaneTitle(string? paneId, string? title)
        => ApplyMutation(clone =>
        {
            var pane = FindPaneLocation(clone.Layout?.RootPane, paneId)?.Pane;
            if (pane == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (pane.Split != null)
            {
                return MutationOutcome.Failure("pane-not-leaf", $"Pane '{pane.PaneId}' is not a leaf pane.");
            }

            if (pane.DashboardItemId is not int dashboardItemId)
            {
                return MutationOutcome.Failure("pane-item-missing", $"Pane '{pane.PaneId}' does not reference a dashboard item.");
            }

            var item = EnsureLayout(clone).Items.FirstOrDefault(candidate => candidate.DashboardItemId == dashboardItemId);
            if (item == null)
            {
                return MutationOutcome.Failure("item-not-found", $"Pane '{pane.PaneId}' references a missing dashboard item.");
            }

            item.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            return MutationOutcome.Success(pane.PaneId);
        });

    public RgfDashboardCommandResult RemovePane(string? paneId)
        => ApplyMutation(clone =>
        {
            var location = FindPaneLocation(clone.Layout?.RootPane, paneId);
            if (location == null)
            {
                return MutationOutcome.Failure("pane-not-found", $"Pane '{paneId}' could not be found.");
            }

            if (location.ParentPane == null || location.ParentSplit == null || location.ChildIndex == null)
            {
                ResetPaneToEmpty(location.Pane);
                return MutationOutcome.Success(location.Pane.PaneId);
            }

            location.ParentSplit.Panes.RemoveAt(location.ChildIndex.Value);

            string? preferredPaneId;
            if (location.ParentSplit.Panes.Count == 0)
            {
                ResetPaneToEmpty(location.ParentPane);
                preferredPaneId = location.ParentPane.PaneId;
            }
            else if (location.ParentSplit.Panes.Count == 1)
            {
                var remainingPane = location.ParentSplit.Panes[0].Pane;
                CollapseToChild(location.ParentPane, remainingPane);
                preferredPaneId = location.ParentPane.PaneId;
            }
            else
            {
                var fallbackIndex = Math.Clamp(location.ChildIndex.Value, 0, location.ParentSplit.Panes.Count - 1);
                preferredPaneId = location.ParentSplit.Panes[fallbackIndex].Pane.PaneId;
            }

            return MutationOutcome.Success(preferredPaneId);
        });

    public RgfDashboardCommandResult ResizeSplit(
        string? parentPaneId,
        string? leadingPaneId,
        string? trailingPaneId,
        decimal leadingSize,
        decimal trailingSize)
        => ApplyMutation(clone =>
        {
            if (!RgfDashboardLayoutMutation.TryResizeSplit(
                clone,
                parentPaneId,
                leadingPaneId,
                trailingPaneId,
                leadingSize,
                trailingSize,
                out var failureCode,
                out var errorMessage))
            {
                return MutationOutcome.Failure(failureCode ?? "split-resize-invalid", errorMessage ?? "Unable to resize split panes.");
            }

            return MutationOutcome.Success();
        });

    private RgfDashboardCommandResult ApplyMutation(Func<RgfDashboardDefinition, MutationOutcome> mutate)
    {
        var clone = Dashboard.DeepCopy() ?? new();
        var outcome = mutate(clone);
        if (!outcome.Succeeded)
        {
            return RgfDashboardCommandResult.Failure(outcome.FailureCode!, outcome.ErrorMessage, ValidationResult);
        }

        clone.Normalize();
        RgfDashboardDefinitionHelper.PruneOrphanItems(clone);

        var validationResult = RgfDashboardLayoutValidator.Validate(clone, RgfDashboardValidationMode.Designer);
        if (!validationResult.IsValid)
        {
            return RgfDashboardCommandResult.Failure(
                "validation-failed",
                validationResult.FirstErrorMessage,
                validationResult);
        }

        Dashboard = clone;
        ValidationResult = validationResult;
        SelectedPaneId = ResolveSelectedPaneId(outcome.PreferredSelectedPaneId ?? SelectedPaneId);
        RefreshDirtyState();

        return RgfDashboardCommandResult.Success(ValidationResult);
    }

    private void RefreshDirtyState()
        => IsDirty = !string.Equals(_baselineSnapshot, Dashboard.SerializeSnapshot(), StringComparison.Ordinal);

    private string? ResolveSelectedPaneId(string? preferredPaneId)
    {
        var normalizedPaneId = NormalizePaneId(preferredPaneId);
        if (normalizedPaneId != null && FindPaneLocation(Dashboard.Layout?.RootPane, normalizedPaneId) != null)
        {
            return normalizedPaneId;
        }

        return Dashboard.Layout?.RootPane?.PaneId;
    }

    private static string? NormalizePaneId(string? paneId)
        => string.IsNullOrWhiteSpace(paneId) ? null : paneId.Trim();

    private static int GetNextDashboardItemId(RgfDashboardDefinition dashboard)
        => EnsureLayout(dashboard).Items
            .Where(item => item != null)
            .Select(item => item.DashboardItemId)
            .DefaultIfEmpty(0)
            .Max() + 1;

    private static RgfDashboardLayout EnsureLayout(RgfDashboardDefinition dashboard)
    {
        dashboard.Layout ??= new();
        dashboard.Layout.Items ??= [];
        dashboard.Layout.RootPane ??= new();
        return dashboard.Layout;
    }

    private static void ResetPaneToEmpty(RgfDashboardPane pane)
    {
        pane.DashboardItemId = null;
        pane.Split = null;
    }

    private static void CollapseToChild(RgfDashboardPane parentPane, RgfDashboardPane childPane)
    {
        parentPane.DashboardItemId = childPane.DashboardItemId;
        parentPane.Split = childPane.Split;
    }

    private static PaneLocation? FindPaneLocation(RgfDashboardPane? rootPane, string? paneId)
    {
        var normalizedPaneId = NormalizePaneId(paneId);
        if (rootPane == null || normalizedPaneId == null)
        {
            return null;
        }

        return FindPaneLocation(rootPane, normalizedPaneId, parentPane: null, parentSplit: null, childIndex: null);
    }

    private static PaneLocation? FindPaneLocation(
        RgfDashboardPane pane,
        string paneId,
        RgfDashboardPane? parentPane,
        RgfDashboardSplit? parentSplit,
        int? childIndex)
    {
        if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
        {
            return new(pane, parentPane, parentSplit, childIndex);
        }

        var split = pane.Split;
        if (split?.Panes == null)
        {
            return null;
        }

        for (var i = 0; i < split.Panes.Count; i++)
        {
            var childPane = split.Panes[i]?.Pane;
            if (childPane == null)
            {
                continue;
            }

            var location = FindPaneLocation(childPane, paneId, pane, split, i);
            if (location != null)
            {
                return location;
            }
        }

        return null;
    }

    private sealed record PaneLocation(
        RgfDashboardPane Pane,
        RgfDashboardPane? ParentPane,
        RgfDashboardSplit? ParentSplit,
        int? ChildIndex);

    private readonly record struct MutationOutcome(bool Succeeded, string? FailureCode, string? ErrorMessage, string? PreferredSelectedPaneId)
    {
        public static MutationOutcome Success(string? preferredSelectedPaneId = null)
            => new(true, null, null, preferredSelectedPaneId);

        public static MutationOutcome Failure(string failureCode, string errorMessage)
            => new(false, failureCode, errorMessage, null);
    }
}
