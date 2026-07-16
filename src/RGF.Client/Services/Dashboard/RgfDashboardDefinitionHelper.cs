using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Models.Dashboard;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Client.Services.Dashboard;

public sealed class RgfDashboardPaneMetadata
{
    public bool HasSplit { get; init; }

    public bool HasMissingItemReference { get; init; }

    public RgfDashboardItem? ResolvedItem { get; init; }

    public string DisplayTitle { get; init; } = "View";

    public RgfDashboardViewType IconKey { get; init; }
}

public sealed class RgfDashboardRenderState
{
    public RgfDashboardDefinition Dashboard { get; init; } = RgfDashboardDefinitionHelper.CreateLocalDashboard();

    public string Snapshot { get; init; } = string.Empty;

    public IReadOnlyDictionary<int, RgfDashboardItem> ItemIndex { get; init; } = new Dictionary<int, RgfDashboardItem>();
}

public static class RgfDashboardDefinitionHelper
{
    public static async Task InitializedAsync(IRecroDictService recroDict, IRecroSecService recroSec)
    {
        if (RecroDictDashboard.Count == 0)
        {
            recroSec.LanguageChangedEvent.Subscribe((_) => LoadRecroDictAsync(recroDict));
            await LoadRecroDictAsync(recroDict);
        }
    }

    public static string GetRgfUiDashboard(this IRecroDictService recroDict, string resourceKey) => recroDict.GetItem(RgfDashboardDefinitionHelper.RecroDictDashboard, resourceKey);

    public static string GetRgfUiDashboard(this IRecroDictService recroDict, string resourceKey, params object[] args) => recroDict.GetItem(RgfDashboardDefinitionHelper.RecroDictDashboard, resourceKey, defaultValue: null, args: args);

    private static ConcurrentDictionary<string, string> RecroDictDashboard = [];

    private static async Task LoadRecroDictAsync(IRecroDictService recroDict)
    {
        RecroDictDashboard = await recroDict.GetDictionaryAsync("RGF.UI.Dashboard", authClient: false);
    }

    public static RgfDashboardDefinition CreateLocalDashboard()
    {
        var dashboard = new RgfDashboardDefinition()
        {
            DashboardId = 0,
            Name = null,
            Description = null,
            RoleId = null,
            LayoutVersion = 1,
            IsReadonly = false,
            Layout = new RgfDashboardLayout()
            {
                Items = [],
                RootPane = new RgfDashboardPane()
            }
        };

        dashboard.Normalize();
        return dashboard;
    }

    public static RgfDashboardDefinition CreateNormalizedCopy(RgfDashboardDefinition? dashboard, bool pruneOrphanItems = false)
    {
        var normalizedDashboard = dashboard.DeepCopy() ?? RgfDashboardDefinitionHelper.CreateLocalDashboard();
        normalizedDashboard.Normalize();

        if (pruneOrphanItems)
        {
            PruneOrphanItems(normalizedDashboard);
        }

        return normalizedDashboard;
    }

    public static RgfDashboardRenderState CreateRenderState(RgfDashboardDefinition? dashboard)
    {
        var normalizedDashboard = CreateNormalizedCopy(dashboard);
        return new()
        {
            Dashboard = normalizedDashboard,
            Snapshot = normalizedDashboard.SerializeSnapshot(),
            ItemIndex = BuildItemIndex(normalizedDashboard)
        };
    }

    public static void Normalize(RgfDashboardDefinition? dashboard)
    {
        if (dashboard == null)
        {
            return;
        }

        dashboard.Name = string.IsNullOrWhiteSpace(dashboard.Name) ? null : dashboard.Name.Trim();
        dashboard.Description = string.IsNullOrWhiteSpace(dashboard.Description) ? null : dashboard.Description.Trim();
        dashboard.RoleId = string.IsNullOrWhiteSpace(dashboard.RoleId) ? null : dashboard.RoleId;
        dashboard.Layout ??= new();
        dashboard.Layout.Width = dashboard.Layout.Width is > 0 ? dashboard.Layout.Width : null;
        dashboard.Layout.Height = dashboard.Layout.Height is > 0 ? dashboard.Layout.Height : null;
        dashboard.Layout.Items ??= [];
        dashboard.Layout.RootPane ??= new();

        foreach (var item in dashboard.Layout.Items)
        {
            if (item == null)
            {
                continue;
            }

            item.Title = string.IsNullOrWhiteSpace(item.Title) ? null : item.Title.Trim();
            item.ViewReference ??= new();
            item.ViewReference.EntityName = string.IsNullOrWhiteSpace(item.ViewReference.EntityName) ? string.Empty : item.ViewReference.EntityName.Trim();
            item.ViewReference.SettingsName = string.IsNullOrWhiteSpace(item.ViewReference.SettingsName) ? null : item.ViewReference.SettingsName.Trim();
        }

        NormalizePane(dashboard.Layout.RootPane, [], new HashSet<RgfDashboardPane>(ReferenceEqualityComparer.Instance));
    }

    public static string SerializeSnapshot(RgfDashboardDefinition? dashboard) => JsonSerializer.Serialize(CreateNormalizedCopy(dashboard), SnapshotJsonOptions);

    public static Dictionary<int, RgfDashboardItem> BuildItemIndex(
        RgfDashboardDefinition? dashboard,
        List<RgfDashboardValidationIssue>? issues = null)
    {
        Dictionary<int, RgfDashboardItem> itemIndex = [];
        var items = dashboard?.Layout?.Items ?? [];

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
            {
                issues?.Add(CreateIssue("item-missing", $"Dashboard item at index {i} is null."));
                continue;
            }

            if (item.DashboardItemId <= 0)
            {
                issues?.Add(CreateIssue(
                    "item-id-invalid",
                    $"Dashboard item '{GetItemName(item, i)}' must have a non-zero positive DashboardItemId.",
                    dashboardItemId: item.DashboardItemId));
                continue;
            }

            if (!itemIndex.TryAdd(item.DashboardItemId, item))
            {
                issues?.Add(CreateIssue(
                    "item-id-duplicate",
                    $"Dashboard item id {item.DashboardItemId} is duplicated in the Items collection.",
                    dashboardItemId: item.DashboardItemId));
            }
        }

        return itemIndex;
    }

    public static void PruneOrphanItems(RgfDashboardDefinition? dashboard)
    {
        if (dashboard?.Layout == null)
        {
            return;
        }

        dashboard.Layout.Items ??= [];
        dashboard.Layout.RootPane ??= new();

        HashSet<int> referencedItemIds = [];
        CollectReferencedItemIds(dashboard.Layout.RootPane, referencedItemIds);
        dashboard.Layout.Items = dashboard.Layout.Items
            .Where(item => item is { DashboardItemId: > 0 } && referencedItemIds.Contains(item.DashboardItemId))
            .ToList();
    }

    public static RgfDashboardPaneMetadata ResolvePaneMetadata(
        RgfDashboardPane? pane,
        IReadOnlyDictionary<int, RgfDashboardItem>? itemIndex)
    {
        if (pane?.Split != null)
        {
            return new()
            {
                HasSplit = true
            };
        }

        if (pane?.DashboardItemId is not int dashboardItemId)
        {
            return new();
        }

        if (itemIndex == null || !itemIndex.TryGetValue(dashboardItemId, out var item))
        {
            return new()
            {
                HasMissingItemReference = true
            };
        }

        return new()
        {
            ResolvedItem = item,
            DisplayTitle = GetDisplayTitle(item),
            IconKey = item.ViewReference?.ViewType ?? RgfDashboardViewType.None
        };
    }

    public static string GetDisplayTitle(RgfDashboardItem? item)
        => item?.Title ?? item?.ViewReference?.SettingsName ?? item?.ViewReference?.EntityName ?? "View";

    public static RgfDashboardPane? FindPane(RgfDashboardPane? pane, string? paneId)
    {
        if (pane == null || string.IsNullOrWhiteSpace(paneId))
        {
            return null;
        }

        if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
        {
            return pane;
        }

        if (pane.Split?.Panes == null)
        {
            return null;
        }

        foreach (var splitPane in pane.Split.Panes)
        {
            var foundPane = FindPane(splitPane?.Pane, paneId);
            if (foundPane != null)
            {
                return foundPane;
            }
        }

        return null;
    }

    private static void NormalizePane(RgfDashboardPane pane, HashSet<string> knownPaneIds, HashSet<RgfDashboardPane> visitedPanes)
    {
        pane.PaneId = CreateUniquePaneId(pane.PaneId, knownPaneIds);

        if (!visitedPanes.Add(pane))
        {
            pane.DashboardItemId = null;
            pane.Split = null;
            return;
        }

        var split = pane.Split;
        if (split == null)
        {
            return;
        }

        pane.DashboardItemId = null;
        split.Panes ??= [];

        for (var i = 0; i < split.Panes.Count; i++)
        {
            var splitPane = split.Panes[i] ??= new();
            splitPane.Size = splitPane.Size > 0 ? splitPane.Size : 1;
            splitPane.MinSize = splitPane.MinSize > 0 ? splitPane.MinSize : null;
            splitPane.Pane ??= new();
        }

        if (split.Panes.Count == 0)
        {
            pane.Split = null;
            return;
        }

        if (split.Panes.Count == 1)
        {
            var childPane = split.Panes[0].Pane;
            NormalizePane(childPane, knownPaneIds, visitedPanes);
            pane.DashboardItemId = childPane.DashboardItemId;
            pane.Split = childPane.Split;
            return;
        }

        foreach (var splitPane in split.Panes)
        {
            NormalizePane(splitPane.Pane, knownPaneIds, visitedPanes);
        }
    }

    private static void CollectReferencedItemIds(RgfDashboardPane? pane, HashSet<int> referencedItemIds)
    {
        if (pane == null)
        {
            return;
        }

        if (pane.Split == null)
        {
            if (pane.DashboardItemId is int itemId)
            {
                referencedItemIds.Add(itemId);
            }

            return;
        }

        foreach (var child in pane.Split.Panes)
        {
            CollectReferencedItemIds(child?.Pane, referencedItemIds);
        }
    }

    private static string CreateUniquePaneId(string? paneId, HashSet<string> knownPaneIds)
    {
        var normalizedPaneId = string.IsNullOrWhiteSpace(paneId) ? null : paneId.Trim();

        if (!string.IsNullOrEmpty(normalizedPaneId) && knownPaneIds.Add(normalizedPaneId))
        {
            return normalizedPaneId;
        }

        string generatedPaneId;
        do
        {
            generatedPaneId = Guid.NewGuid().ToString("N");
        }
        while (!knownPaneIds.Add(generatedPaneId));

        return generatedPaneId;
    }

    private static string GetItemName(RgfDashboardItem item, int index = -1)
    {
        var name = GetDisplayTitle(item);
        return !string.IsNullOrWhiteSpace(name)
            ? name
            : index >= 0 ? $"Item {index}" : "Item";
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

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
