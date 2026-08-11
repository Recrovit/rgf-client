using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Services.Dashboard;

namespace Recrovit.RecroGridFramework.Client.Tests.Services.Dashboard;

public sealed class RgfDashboardDesignerStateTests
{
    [Fact]
    public void GetDisplayTitle_PrefersExplicitTitle()
    {
        var item = new RgfDashboardItem
        {
            Title = " Orders title ",
            ViewReference = CreateViewReference("Orders", 12, "Orders default")
        };

        var title = RgfDashboardDefinitionHelper.GetDisplayTitle(item);

        Assert.Equal("Orders title", title);
    }

    [Fact]
    public void GetDisplayTitle_FallsBackToSettingsName_WhenTitleIsBlank()
    {
        var item = new RgfDashboardItem
        {
            Title = "   ",
            ViewReference = CreateViewReference("Orders", 12, " Orders default ")
        };

        var title = RgfDashboardDefinitionHelper.GetDisplayTitle(item);

        Assert.Equal("Orders default", title);
    }

    [Fact]
    public void GetDisplayTitle_FallsBackToEntityName_WhenTitleAndSettingsNameAreBlank()
    {
        var item = new RgfDashboardItem
        {
            Title = null,
            ViewReference = CreateViewReference(" Orders ", 12, "   ")
        };

        var title = RgfDashboardDefinitionHelper.GetDisplayTitle(item);

        Assert.Equal("Orders", title);
    }

    [Fact]
    public void AssignItem_LeavesDashboardInvalid_WhenSettingsNameIsBlank()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;

        var result = state.AssignItem(rootPaneId, CreateViewReference("Orders", 12, "   "));

        Assert.False(result.Succeeded);
        Assert.Equal("validation-failed", result.FailureCode);
        Assert.Contains(result.ValidationResult.Issues, issue => issue.Code == "item-settings-name-missing");
        Assert.True(state.ValidationResult.IsValid);
    }

    [Fact]
    public void Load_SelectsRootPaneAndResetsDirtyState()
    {
        var state = new RgfDashboardDesignerState();
        var dashboard = new RgfDashboardDefinition
        {
            Name = "  Sample  ",
            Layout = new()
            {
                RootPane = new() { PaneId = " root " },
                Items = null!
            }
        };

        state.Load(dashboard, isNameEditable: true, isReadonly: false);

        Assert.Equal("Sample", state.Dashboard.Name);
        Assert.NotNull(state.Dashboard.Layout.Items);
        Assert.Equal(state.Dashboard.Layout.RootPane.PaneId, state.SelectedPaneId);
        Assert.True(state.ValidationResult.IsValid);
        Assert.False(state.IsDirty);
        Assert.True(state.IsNameEditable);
        Assert.False(state.IsReadonly);
    }

    [Fact]
    public void SelectPane_KeepsSelectionUnchanged_WhenPaneDoesNotExist()
    {
        var state = CreateLoadedState();
        var originalPaneId = state.SelectedPaneId;

        var result = state.SelectPane("missing-pane");

        Assert.False(result.Succeeded);
        Assert.Equal("pane-not-found", result.FailureCode);
        Assert.Equal(originalPaneId, state.SelectedPaneId);
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void SplitPane_CreatesRequestedChildCount_AndMarksDirty()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;

        var result = state.SplitPane(rootPaneId, RgfDashboardSplitDirection.Columns, 3);

        Assert.True(result.Succeeded);
        Assert.True(state.IsDirty);
        Assert.NotNull(state.Dashboard.Layout.RootPane.Split);
        Assert.Equal(3, state.Dashboard.Layout.RootPane.Split.Panes.Count);
        Assert.All(state.Dashboard.Layout.RootPane.Split.Panes, splitPane => Assert.Equal(1m, splitPane.Size));
        Assert.All(state.Dashboard.Layout.RootPane.Split.Panes, splitPane => Assert.False(string.IsNullOrWhiteSpace(splitPane.Pane.PaneId)));
        Assert.Equal(state.Dashboard.Layout.RootPane.Split.Panes[0].Pane.PaneId, state.SelectedPaneId);
    }

    [Fact]
    public void SplitPane_FailsForSplitPane()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 2);

        var result = state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Rows, 2);

        Assert.False(result.Succeeded);
        Assert.Equal("pane-not-leaf", result.FailureCode);
    }

    [Fact]
    public void SetSplitPaneCount_IncreasesChildCount_ByAppendingLeafPanes()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 2);
        var originalPaneIds = state.Dashboard.Layout.RootPane.Split!.Panes.Select(splitPane => splitPane.Pane.PaneId).ToArray();

        var result = state.SetSplitPaneCount(state.Dashboard.Layout.RootPane.PaneId, 4);

        Assert.True(result.Succeeded);
        Assert.Equal(4, state.Dashboard.Layout.RootPane.Split.Panes.Count);
        Assert.Equal(originalPaneIds, state.Dashboard.Layout.RootPane.Split.Panes.Take(2).Select(splitPane => splitPane.Pane.PaneId).ToArray());
        Assert.All(state.Dashboard.Layout.RootPane.Split.Panes.Skip(2), splitPane =>
        {
            Assert.Equal(1m, splitPane.Size);
            Assert.Null(splitPane.Pane.DashboardItemId);
            Assert.Null(splitPane.Pane.Split);
            Assert.False(string.IsNullOrWhiteSpace(splitPane.Pane.PaneId));
        });
    }

    [Fact]
    public void SetSplitPaneCount_DecreasesChildCount_ByRemovingTrailingPanes()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 4);
        var paneIds = state.Dashboard.Layout.RootPane.Split!.Panes.Select(splitPane => splitPane.Pane.PaneId).ToArray();

        var result = state.SetSplitPaneCount(state.Dashboard.Layout.RootPane.PaneId, 2);

        Assert.True(result.Succeeded);
        Assert.Equal(2, state.Dashboard.Layout.RootPane.Split.Panes.Count);
        Assert.Equal(paneIds.Take(2), state.Dashboard.Layout.RootPane.Split.Panes.Select(splitPane => splitPane.Pane.PaneId));
    }

    [Fact]
    public void SetSplitPaneCount_PrunesItemsRemovedWithTrailingPanes()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 4);
        var secondPaneId = state.Dashboard.Layout.RootPane.Split!.Panes[1].Pane.PaneId;
        var fourthPaneId = state.Dashboard.Layout.RootPane.Split.Panes[3].Pane.PaneId;
        state.AssignItem(secondPaneId, CreateViewReference("Orders", 12, "Keep"));
        state.AssignItem(fourthPaneId, CreateViewReference("Invoices", 13, "Drop"));

        var result = state.SetSplitPaneCount(state.Dashboard.Layout.RootPane.PaneId, 2);

        Assert.True(result.Succeeded);
        Assert.Single(state.Dashboard.Layout.Items);
        Assert.Null(state.Dashboard.Layout.Items[0].Title);
        Assert.Equal("Keep", RgfDashboardDefinitionHelper.GetDisplayTitle(state.Dashboard.Layout.Items[0]));
        Assert.Equal(state.Dashboard.Layout.Items[0].DashboardItemId, state.Dashboard.Layout.RootPane.Split.Panes[1].Pane.DashboardItemId);
    }

    [Fact]
    public void SetSplitPaneCount_SucceedsWithoutChanges_WhenCountMatchesCurrentCount()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 3);
        var originalSnapshot = state.Dashboard.SerializeSnapshot();
        var originalDirtyState = state.IsDirty;

        var result = state.SetSplitPaneCount(state.Dashboard.Layout.RootPane.PaneId, 3);

        Assert.True(result.Succeeded);
        Assert.True(state.ValidationResult.IsValid);
        Assert.Equal(originalSnapshot, state.Dashboard.SerializeSnapshot());
        Assert.Equal(originalDirtyState, state.IsDirty);
    }

    [Fact]
    public void SetSplitPaneCount_FailsForLeafPane()
    {
        var state = CreateLoadedState();

        var result = state.SetSplitPaneCount(state.Dashboard.Layout.RootPane.PaneId, 3);

        Assert.False(result.Succeeded);
        Assert.Equal("pane-not-split", result.FailureCode);
    }

    [Fact]
    public void AssignItem_CreatesNewPositiveItem_ForEmptyLeaf()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;

        var result = state.AssignItem(rootPaneId, CreateViewReference("Orders", 12, "Main grid"));

        Assert.True(result.Succeeded);
        Assert.True(state.ValidationResult.IsValid);
        Assert.Single(state.Dashboard.Layout.Items);
        Assert.Equal(1, state.Dashboard.Layout.Items[0].DashboardItemId);
        Assert.Equal(state.Dashboard.Layout.Items[0].DashboardItemId, state.Dashboard.Layout.RootPane.DashboardItemId);
        Assert.Equal("Orders", state.Dashboard.Layout.Items[0].ViewReference.EntityName);
        Assert.Null(state.Dashboard.Layout.Items[0].Title);
        Assert.Equal("Main grid", RgfDashboardDefinitionHelper.GetDisplayTitle(state.Dashboard.Layout.Items[0]));
    }

    [Fact]
    public void AssignItem_CreatesIncreasingPositiveIds_ForMultipleNewItems()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 3);
        var firstPaneId = state.Dashboard.Layout.RootPane.Split!.Panes[0].Pane.PaneId;
        var secondPaneId = state.Dashboard.Layout.RootPane.Split.Panes[1].Pane.PaneId;
        var thirdPaneId = state.Dashboard.Layout.RootPane.Split.Panes[2].Pane.PaneId;

        state.AssignItem(firstPaneId, CreateViewReference("Orders", 12, "Orders"));
        state.AssignItem(secondPaneId, CreateViewReference("Invoices", 13, "Invoices"));
        state.AssignItem(thirdPaneId, CreateViewReference("Products", 14, "Products"));

        Assert.Equal([1, 2, 3], state.Dashboard.Layout.Items.Select(item => item.DashboardItemId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void AssignItem_UpdatesExistingItem_WhenPaneAlreadyHasItem()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;
        state.AssignItem(rootPaneId, CreateViewReference("Orders", 12, "Main grid"));
        var originalItemId = state.Dashboard.Layout.Items[0].DashboardItemId;

        var result = state.AssignItem(rootPaneId, CreateViewReference("Invoices", 34, "Chart"));

        Assert.True(result.Succeeded);
        Assert.Single(state.Dashboard.Layout.Items);
        Assert.Equal(originalItemId, state.Dashboard.Layout.Items[0].DashboardItemId);
        Assert.Equal("Invoices", state.Dashboard.Layout.Items[0].ViewReference.EntityName);
        Assert.Null(state.Dashboard.Layout.Items[0].Title);
        Assert.Equal("Chart", RgfDashboardDefinitionHelper.GetDisplayTitle(state.Dashboard.Layout.Items[0]));
    }

    [Fact]
    public void Load_PreservesDashboardItemIds_ForClonedDashboard()
    {
        var clone = new RgfDashboardDefinition
        {
            DashboardId = 5,
            Name = "Original",
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    DashboardItemId = 12
                },
                Items =
                [
                    new()
                    {
                        DashboardItemId = 12,
                        Title = "Orders",
                        ViewReference = CreateViewReference("Orders", 12, "Orders")
                    }
                ]
            }
        }.CreateClone();

        var state = new RgfDashboardDesignerState();
        state.Load(clone);

        Assert.True(state.ValidationResult.IsValid);
        Assert.Single(state.Dashboard.Layout.Items);
        Assert.Equal(12, state.Dashboard.Layout.Items[0].DashboardItemId);
        Assert.Equal(state.Dashboard.Layout.Items[0].DashboardItemId, state.Dashboard.Layout.RootPane.DashboardItemId);
    }

    [Fact]
    public void AssignItem_UsesNextIdAfterExistingMaximum_InClonedDashboard()
    {
        var clone = new RgfDashboardDefinition
        {
            DashboardId = 5,
            Name = "Original",
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    Split = new()
                    {
                        Direction = RgfDashboardSplitDirection.Columns,
                        Panes =
                        [
                            new()
                            {
                                Pane = new()
                                {
                                    PaneId = "left",
                                    DashboardItemId = 12
                                }
                            },
                            new()
                            {
                                Pane = new()
                                {
                                    PaneId = "right"
                                }
                            }
                        ]
                    }
                },
                Items =
                [
                    new()
                    {
                        DashboardItemId = 12,
                        Title = "Orders",
                        ViewReference = CreateViewReference("Orders", 12, "Orders")
                    }
                ]
            }
        }.CreateClone();

        var state = new RgfDashboardDesignerState();
        state.Load(clone);

        var result = state.AssignItem("right", CreateViewReference("Invoices", 34, "Invoices"));

        Assert.True(result.Succeeded);
        Assert.Equal([12, 13], state.Dashboard.Layout.Items.Select(item => item.DashboardItemId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void AssignItem_FailsForSplitPane()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;
        state.SplitPane(rootPaneId, RgfDashboardSplitDirection.Columns, 2);

        var result = state.AssignItem(rootPaneId, CreateViewReference("Orders", 12, "Main grid"));

        Assert.False(result.Succeeded);
        Assert.Equal("pane-not-leaf", result.FailureCode);
        Assert.Empty(state.Dashboard.Layout.Items);
    }

    [Fact]
    public void RemovePane_CollapsesParentSplit_WhenOneChildRemains()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 2);
        var leftPaneId = state.Dashboard.Layout.RootPane.Split!.Panes[0].Pane.PaneId;
        var rightPaneId = state.Dashboard.Layout.RootPane.Split.Panes[1].Pane.PaneId;
        state.AssignItem(leftPaneId, CreateViewReference("Orders", 12, "Left"));
        state.AssignItem(rightPaneId, CreateViewReference("Invoices", 13, "Right"));

        var result = state.RemovePane(leftPaneId);

        Assert.True(result.Succeeded);
        Assert.Null(state.Dashboard.Layout.RootPane.Split);
        Assert.NotNull(state.Dashboard.Layout.RootPane.DashboardItemId);
        Assert.Single(state.Dashboard.Layout.Items);
        Assert.Null(state.Dashboard.Layout.Items[0].Title);
        Assert.Equal("Right", RgfDashboardDefinitionHelper.GetDisplayTitle(state.Dashboard.Layout.Items[0]));
        Assert.Equal(state.Dashboard.Layout.RootPane.PaneId, state.SelectedPaneId);
    }

    [Fact]
    public void RemovePane_RootFallbacksToEmptyLeaf()
    {
        var state = CreateLoadedState();
        var rootPaneId = state.Dashboard.Layout.RootPane.PaneId;
        state.AssignItem(rootPaneId, CreateViewReference("Orders", 12, "Main grid"));

        var result = state.RemovePane(rootPaneId);

        Assert.True(result.Succeeded);
        Assert.Null(state.Dashboard.Layout.RootPane.Split);
        Assert.Null(state.Dashboard.Layout.RootPane.DashboardItemId);
        Assert.Empty(state.Dashboard.Layout.Items);
        Assert.Equal(rootPaneId, state.SelectedPaneId);
    }

    [Fact]
    public void ResizeSplit_UpdatesAdjacentPaneSizes()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 3);
        var parentPaneId = state.Dashboard.Layout.RootPane.PaneId;
        var leftPaneId = state.Dashboard.Layout.RootPane.Split!.Panes[0].Pane.PaneId;
        var middlePaneId = state.Dashboard.Layout.RootPane.Split.Panes[1].Pane.PaneId;
        var rightPaneId = state.Dashboard.Layout.RootPane.Split.Panes[2].Pane.PaneId;

        var result = state.ResizeSplit(parentPaneId, middlePaneId, rightPaneId, 2.5m, 0.5m);

        Assert.True(result.Succeeded);
        Assert.Equal(1m, state.Dashboard.Layout.RootPane.Split.Panes[0].Size);
        Assert.Equal(2.5m, state.Dashboard.Layout.RootPane.Split.Panes[1].Size);
        Assert.Equal(0.5m, state.Dashboard.Layout.RootPane.Split.Panes[2].Size);
    }

    [Fact]
    public void ResizeSplit_FailsForNonAdjacentPanes()
    {
        var state = CreateLoadedState();
        state.SplitPane(state.Dashboard.Layout.RootPane.PaneId, RgfDashboardSplitDirection.Columns, 3);
        var parentPaneId = state.Dashboard.Layout.RootPane.PaneId;
        var leftPaneId = state.Dashboard.Layout.RootPane.Split!.Panes[0].Pane.PaneId;
        var rightPaneId = state.Dashboard.Layout.RootPane.Split.Panes[2].Pane.PaneId;

        var result = state.ResizeSplit(parentPaneId, leftPaneId, rightPaneId, 2m, 1m);

        Assert.False(result.Succeeded);
        Assert.Equal("resize-split-pane-not-adjacent", result.FailureCode);
    }

    [Fact]
    public void DirtyState_IsManagedByDesignerState()
    {
        var state = CreateLoadedState();

        Assert.False(state.IsDirty);

        var result = state.SetName(" Dashboard ");

        Assert.True(result.Succeeded);
        Assert.False(state.IsDirty);

        result = state.SetName("  Updated  ");

        Assert.True(result.Succeeded);
        Assert.True(state.IsDirty);

        result = state.SetName("Updated");

        Assert.True(result.Succeeded);
        Assert.True(state.IsDirty);

        state.Load(state.Dashboard);

        Assert.False(state.IsDirty);
    }

    private static RgfDashboardDesignerState CreateLoadedState()
    {
        var state = new RgfDashboardDesignerState();
        state.Load(new RgfDashboardDefinition
        {
            Name = "Dashboard",
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root"
                },
                Items = []
            }
        });

        return state;
    }

    private static RgfDashboardViewReference CreateViewReference(string entityName, int settingsId, string settingsName)
        => new()
        {
            EntityName = entityName,
            ViewType = RgfDashboardViewType.Grid,
            SettingsId = settingsId,
            SettingsName = settingsName ?? string.Empty
        };
}
