using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Dashboard;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Parameters;
using Recrovit.RecroGridFramework.Client.Services.Dashboard;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class RgfDashboardDesignerComponentTests
{
    [Fact]
    public void SplitCountCombo_UsesSelectedSplitPaneChildCount()
    {
        using var testContext = CreateTestContext();
        RgfDashboardDefinition? editedDashboard = null;
        var dashboard = new RgfDashboardDefinition
        {
            Name = "Dashboard",
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
                            new() { Pane = new() { PaneId = "pane-1" } },
                            new() { Pane = new() { PaneId = "pane-2" } },
                            new() { Pane = new() { PaneId = "pane-3" } },
                            new() { Pane = new() { PaneId = "pane-4" } }
                        ]
                    }
                },
                Items = []
            }
        };

        var cut = testContext.Render<DashboardDesignerComponent>(parameters => parameters
            .Add(component => component.Parameters, new RgfDashboardDesignerParameters
            {
                Dashboard = dashboard,
                EntityOptions =
                [
                    new()
                    {
                        EntityId = 1,
                        EntityName = "Orders",
                        Title = "Orders"
                    }
                ],
                DashboardEdited = EventCallback.Factory.Create<RgfDashboardDefinition>(this, dashboard => editedDashboard = dashboard)
            }));

        var splitLabel = cut.FindAll("label").Single(label => label.TextContent.Trim() == "Split");
        var splitSelectId = splitLabel.GetAttribute("for");
        var splitSelect = cut.Find($"select#{splitSelectId}");

        Assert.Equal("4", splitSelect.GetAttribute("value"));
        Assert.Equal("4", splitSelect.QuerySelector("option[selected]")?.GetAttribute("value"));

        splitSelect.Change(3);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(editedDashboard);
            Assert.Equal(3, editedDashboard!.Layout.RootPane.Split!.Panes.Count);
            Assert.Equal("3", cut.Find($"select#{splitSelectId}").GetAttribute("value"));
        });
    }

    [Fact]
    public void PanelHeaderToggle_RendersBeforeTitle_AndDisablesTitleWhenHeaderIsHidden()
    {
        using var testContext = CreateTestContext();

        var dashboard = CreateLeafDashboard(new RgfDashboardItem
        {
            DashboardItemId = 1,
            Title = "Orders title",
            ShowHeader = false,
            ViewReference = new()
            {
                EntityName = "Orders",
                ViewType = RgfDashboardViewType.Grid,
                SettingsId = 42,
                SettingsName = "Orders default"
            }
        });

        var hiddenHeaderCut = RenderDesigner(testContext, dashboard);

        var labels = hiddenHeaderCut.FindAll("label").Select(label => label.TextContent.Trim()).ToList();
        Assert.Contains("PanelHeaderVisible", labels);
        Assert.Contains("PanelTitle", labels);
        Assert.True(labels.IndexOf("PanelHeaderVisible") < labels.IndexOf("PanelTitle"));

        var titleInput = FindLabeledControl(hiddenHeaderCut, "PanelTitle");
        Assert.NotNull(titleInput.GetAttribute("disabled"));

        dashboard = CreateLeafDashboard(new RgfDashboardItem
        {
            DashboardItemId = 1,
            Title = "Orders title",
            ShowHeader = true,
            ViewReference = new()
            {
                EntityName = "Orders",
                ViewType = RgfDashboardViewType.Grid,
                SettingsId = 42,
                SettingsName = "Orders default"
            }
        });

        var visibleHeaderCut = RenderDesigner(testContext, dashboard);
        titleInput = FindLabeledControl(visibleHeaderCut, "PanelTitle");
        Assert.Null(titleInput.GetAttribute("disabled"));
    }

    [Fact]
    public void SaveButton_IsControlledByParametersCanSave()
    {
        using var testContext = CreateTestContext();
        var saveRequestedCount = 0;
        void SaveRequested() => saveRequestedCount++;

        var disabledCut = RenderDesigner(testContext, CreateLeafDashboard(), canSave: false, saveRequested: SaveRequested);

        var saveButton = FindButton(disabledCut, "bi-floppy");
        Assert.NotNull(saveButton.GetAttribute("disabled"));
        saveButton.Click();
        Assert.Equal(0, saveRequestedCount);

        var enabledCut = RenderDesigner(testContext, CreateLeafDashboard(), canSave: true, saveRequested: SaveRequested);

        saveButton = FindButton(enabledCut, "bi-floppy");
        Assert.Null(saveButton.GetAttribute("disabled"));
        saveButton.Click();
        Assert.Equal(1, saveRequestedCount);
    }

    private static BunitContext CreateTestContext()
    {
        var testContext = new BunitContext();
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        testContext.Services.AddSingleton<IRgfApiService, FakeRgfApiService>();
        var recroDict = testContext.Services.GetRequiredService<IRecroDictService>();
        var recroSec = testContext.Services.GetRequiredService<IRecroSecService>();
        RgfDashboardDefinitionHelper.InitializedAsync(recroDict, recroSec).GetAwaiter().GetResult();
        return testContext;
    }

    private static IRenderedComponent<DashboardDesignerComponent> RenderDesigner(
        BunitContext testContext,
        RgfDashboardDefinition dashboard,
        bool canSave = false,
        bool canDelete = false,
        Action? saveRequested = null)
        => testContext.Render<DashboardDesignerComponent>(parameters => parameters
            .Add(component => component.Parameters, CreateParameters(dashboard, canSave, canDelete, saveRequested)));

    private static RgfDashboardDesignerParameters CreateParameters(
        RgfDashboardDefinition dashboard,
        bool canSave = false,
        bool canDelete = false,
        Action? saveRequested = null)
        => new()
        {
            Dashboard = dashboard,
            EntityOptions =
            [
                new()
                {
                    EntityId = 1,
                    EntityName = "Orders",
                    Title = "Orders"
                }
            ],
            CanSave = canSave,
            CanDelete = canDelete,
            SaveRequested = EventCallback.Factory.Create(new object(), saveRequested ?? (() => { })),
            DashboardEdited = EventCallback.Factory.Create<RgfDashboardDefinition>(new object(), (Action<RgfDashboardDefinition>)(_ => { }))
        };

    private static RgfDashboardDefinition CreateLeafDashboard(RgfDashboardItem? item = null)
    {
        var dashboard = new RgfDashboardDefinition
        {
            Name = "Dashboard",
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    DashboardItemId = item?.DashboardItemId
                },
                Items = item == null ? [] : [item]
            }
        };

        return dashboard;
    }

    private static IElement FindLabeledControl(IRenderedComponent<DashboardDesignerComponent> cut, string labelText)
    {
        var label = cut.FindAll("label").Single(candidate => candidate.TextContent.Trim() == labelText);
        var elementId = label.GetAttribute("for");
        Assert.False(string.IsNullOrWhiteSpace(elementId));
        return cut.Find($"#{elementId}");
    }

    private static IElement FindButton(IRenderedComponent<DashboardDesignerComponent> cut, string iconClass)
        => cut.FindAll("button").Single(button => button.QuerySelector($".{iconClass}") != null);

    private sealed class FakeRecroSecService : IRecroSecService
    {
        public EventDispatcher<EventArgs> AuthenticationStateChanged { get; } = new();

        public EventDispatcher<DataEventArgs<RgfUserState>> UserStateChangedEvent { get; } = new();

        public string? UserName => null;

        public bool IsAuthenticated => false;

        public bool IsAdmin => UserState.IsAdmin;

        public List<string> RoleClaim { get; } = [];

        public ClaimsPrincipal CurrentUser { get; } = new(new ClaimsIdentity());

        public RgfUserState UserState { get; } = new();

        public IReadOnlyDictionary<string, string> Roles => UserState.Roles ?? new Dictionary<string, string>();

        public string UserLanguage => "eng";

        public EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; } = new();

        public Task<string?> GetAccessTokenAsync()
            => Task.FromResult<string?>(null);

        public Task<string?> SetUserLanguageAsync(string? language)
            => Task.FromResult<string?>(null);

        public Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings)
            => Task.FromResult(false);

        public Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60)
            => throw new NotSupportedException();

        public Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60)
            => throw new NotSupportedException();

        public Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60)
            => throw new NotSupportedException();
    }
}
