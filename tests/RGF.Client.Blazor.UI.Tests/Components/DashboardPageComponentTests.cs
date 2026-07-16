using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Dashboard;
using System.Net;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class DashboardPageComponentTests : IDisposable
{
    public DashboardPageComponentTests()
    {
        RgfClientBlazorUiTestState.Reset();
    }

    public void Dispose()
    {
        RgfClientBlazorUiTestState.Reset();
    }

    [Fact]
    public void OnInitialized_LoadsCatalog_AndRendersSelectedDashboard()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        using var testContext = DashboardPageTestServices.CreateContext(apiService);

        var cut = testContext.Render<DashboardPageComponent>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Orders dashboard", cut.Markup);
            Assert.Contains("rgf-dashboard-runtime", cut.Markup);
        });
        Assert.Contains(apiService.Requests, request => request.Uri == "/rgf/api/dashboard/catalog");
    }

    [Fact]
    public void CreateDashboard_OpensDesigner_WithNoSelection()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.SaveDashboardResult = CreateSavedDashboard(8, "New dashboard");
        apiService.DashboardCatalogResults.Enqueue(apiService.DashboardCatalogResult);
        apiService.DashboardCatalogResults.Enqueue(new RgfDashboardCatalogResult
        {
            Dashboards = [new() { DashboardId = 8, Name = "New dashboard" }]
        });
        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        ClickToolbarButton(cut, "bi-plus-lg");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("rgf-dashboard-designer", cut.Markup);
            Assert.DoesNotContain("rgf-dashboard-runtime", cut.Markup);
            Assert.Null(GetDashboardSelector(cut).GetAttribute("value"));
        });

        var saveButton = FindButton(cut, "bi-floppy");
        Assert.NotNull(saveButton.GetAttribute("disabled"));

        SetInputValue(cut, "DashboardName", "New dashboard");

        cut.WaitForAssertion(() =>
        {
            saveButton = FindButton(cut, "bi-floppy");
            Assert.Null(saveButton.GetAttribute("disabled"));
        });

        ClickDesignerButton(cut, "bi-floppy");

        cut.WaitForAssertion(() => Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup));
        Assert.NotNull(apiService.LastSavedDashboardPayload);
        Assert.True(apiService.LastSavedDashboardPayload!.DashboardId <= 0);
    }

    [Fact]
    public void CloneDashboard_OpensDesigner_WithEditableName()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.SaveDashboardResult = CreateSavedDashboard(8, "Cloned dashboard");
        apiService.DashboardCatalogResults.Enqueue(apiService.DashboardCatalogResult);
        apiService.DashboardCatalogResults.Enqueue(new RgfDashboardCatalogResult
        {
            Dashboards = [new() { DashboardId = 8, Name = "Cloned dashboard" }]
        });
        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();
        WaitForLoadedDashboard(cut);

        ClickToolbarButton(cut, "bi-copy");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("rgf-dashboard-designer", cut.Markup);
            Assert.Equal(string.Empty, FindLabeledControl(cut, "DashboardName").GetAttribute("value") ?? string.Empty);
            Assert.Null(FindLabeledControl(cut, "DashboardName").GetAttribute("readonly"));
        });

        SetInputValue(cut, "DashboardName", "Cloned dashboard");
        ClickDesignerButton(cut, "bi-floppy");

        cut.WaitForAssertion(() => Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup));
        Assert.NotNull(apiService.LastSavedDashboardPayload);
        Assert.NotEqual(7, apiService.LastSavedDashboardPayload!.DashboardId);
        Assert.True(apiService.LastSavedDashboardPayload.DashboardId <= 0);
    }

    [Fact]
    public void EditDashboard_TogglesOnlyWhenNotDirty_AndKeepsDesignerOpenWhenDirty()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();
        WaitForLoadedDashboard(cut);

        ClickToolbarButton(cut, "bi-pencil");
        cut.WaitForAssertion(() => Assert.Contains("rgf-dashboard-designer", cut.Markup));
        Assert.NotNull(FindLabeledControl(cut, "DashboardName").GetAttribute("readonly"));

        ClickToolbarButton(cut, "bi-pencil");
        cut.WaitForAssertion(() => Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup));

        ClickToolbarButton(cut, "bi-pencil");
        cut.WaitForAssertion(() => Assert.Contains("rgf-dashboard-designer", cut.Markup));
        SetInputValue(cut, "Description", "Updated description");

        cut.WaitForAssertion(() =>
        {
            var saveButton = FindButton(cut, "bi-floppy");
            Assert.Null(saveButton.GetAttribute("disabled"));
        });

        ClickToolbarButton(cut, "bi-pencil");
        cut.WaitForAssertion(() => Assert.Contains("rgf-dashboard-designer", cut.Markup));
    }

    [Fact]
    public void CreateDashboard_DisablesOnlyRoleSetting_WhenPublicDashboardSettingIsNotAllowed()
    {
        var apiService = CreateApiServiceWithSelectedDashboard(isPublicDashboardSettingAllowed: false);
        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        ClickToolbarButton(cut, "bi-plus-lg");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("rgf-dashboard-designer", cut.Markup);
            Assert.Null(FindLabeledControl(cut, "DashboardName").GetAttribute("readonly"));
            Assert.NotNull(FindDashboardRoleSelector(cut).GetAttribute("disabled"));
        });

        SetInputValue(cut, "DashboardName", "New dashboard");

        cut.WaitForAssertion(() => Assert.Null(FindButton(cut, "bi-floppy").GetAttribute("disabled")));
    }

    [Fact]
    public void EditDashboard_DisablesOnlyRoleSetting_WhenPublicDashboardSettingIsNotAllowed()
    {
        var apiService = CreateApiServiceWithSelectedDashboard(isPublicDashboardSettingAllowed: false);
        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();
        WaitForLoadedDashboard(cut);

        ClickToolbarButton(cut, "bi-pencil");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("rgf-dashboard-designer", cut.Markup);
            Assert.Null(FindLabeledControl(cut, "Description").GetAttribute("readonly"));
            Assert.NotNull(FindDashboardRoleSelector(cut).GetAttribute("disabled"));
            Assert.Null(FindButton(cut, "bi-trash").GetAttribute("disabled"));
        });

        SetInputValue(cut, "Description", "Updated description");

        cut.WaitForAssertion(() => Assert.Null(FindButton(cut, "bi-floppy").GetAttribute("disabled")));
    }

    [Fact]
    public void SaveDashboard_SendsNormalizedPayload_ReloadsCatalog_AndClosesDesigner()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.SaveDashboardResult = CreateSavedDashboard(8, "  Saved dashboard  ");
        apiService.DashboardCatalogResults.Enqueue(apiService.DashboardCatalogResult);
        apiService.DashboardCatalogResults.Enqueue(new RgfDashboardCatalogResult
        {
            Dashboards = [new() { DashboardId = 8, Name = "Saved dashboard" }]
        });

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        ClickToolbarButton(cut, "bi-plus-lg");
        SetInputValue(cut, "DashboardName", "  Saved dashboard  ");
        ClickDesignerButton(cut, "bi-floppy");

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup);
            Assert.Contains("rgf-dashboard-runtime", cut.Markup);
        });

        Assert.Equal("Saved dashboard", apiService.LastSavedDashboardPayload?.Name);
        Assert.Equal(2, apiService.Requests.Count(request => request.Uri == "/rgf/api/dashboard/catalog"));
        Assert.Contains(apiService.Requests, request => request.Uri == "/rgf/api/dashboard/save");
    }

    [Fact]
    public void SaveDashboard_ShowsError_WhenApiReturnsFailure()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.SaveDashboardSuccess = false;
        apiService.SaveDashboardErrorMessage = "Save failed";

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        ClickToolbarButton(cut, "bi-plus-lg");
        SetInputValue(cut, "DashboardName", "Broken");
        ClickDesignerButton(cut, "bi-floppy");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Save failed", cut.Markup);
            Assert.DoesNotContain("rgf-dashboard-runtime", cut.Markup);
        });
    }

    [Fact]
    public void DeleteDashboard_AfterConfirmation_ClearsSelection_AndReloadsCatalog()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.DashboardCatalogResults.Enqueue(apiService.DashboardCatalogResult);
        apiService.DashboardCatalogResults.Enqueue(new RgfDashboardCatalogResult
        {
            Dashboards = []
        });

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();
        WaitForLoadedDashboard(cut);

        ClickToolbarButton(cut, "bi-pencil");
        cut.WaitForAssertion(() => Assert.Contains("rgf-dashboard-designer", cut.Markup));
        ClickDesignerButton(cut, "bi-trash");
        cut.WaitForAssertion(() => Assert.Contains("Delete - Orders dashboard", cut.Markup));
        ClickButtonByText(cut, "Yes");

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup);
            Assert.DoesNotContain("rgf-dashboard-runtime", cut.Markup);
        });

        Assert.Equal(2, apiService.Requests.Count(request => request.Uri == "/rgf/api/dashboard/catalog"));
        Assert.Equal(7, apiService.LastDeletedDashboardPayload?.DashboardId);
    }

    [Fact]
    public void DeleteDashboard_ShowsError_WhenApiReturnsFailure()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.DeleteDashboardSuccess = false;
        apiService.DeleteDashboardErrorMessage = "Delete failed";

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();
        WaitForLoadedDashboard(cut);

        ClickToolbarButton(cut, "bi-pencil");
        ClickDesignerButton(cut, "bi-trash");
        cut.WaitForAssertion(() => Assert.Contains("Delete - Orders dashboard", cut.Markup));
        ClickButtonByText(cut, "Yes");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Delete failed", cut.Markup);
            Assert.DoesNotContain("NoDashboardSelected", cut.Markup);
        });
    }

    [Fact]
    public void LoadDashboard_ClearsSelectionWithoutError_WhenDashboardIsNotFound()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.DashboardLoadSuccess = false;
        apiService.DashboardLoadStatusCode = HttpStatusCode.NotFound;
        apiService.DashboardResult = null;

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        var selector = GetDashboardSelector(cut);
        selector.Change(99);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("text-danger", cut.Markup);
            Assert.DoesNotContain("rgf-dashboard-designer", cut.Markup);
            Assert.DoesNotContain("rgf-dashboard-runtime", cut.Markup);
        });
    }

    [Fact]
    public void LoadDashboard_ShowsError_WhenApiThrows()
    {
        var apiService = CreateApiServiceWithSelectedDashboard();
        apiService.DashboardLoadException = new InvalidOperationException("Load exploded");

        using var testContext = DashboardPageTestServices.CreateContext(apiService);
        var cut = testContext.Render<DashboardPageComponent>();

        var selector = GetDashboardSelector(cut);
        selector.Change(99);

        cut.WaitForAssertion(() => Assert.Contains("Load exploded", cut.Markup));
    }

    private static FakeRgfApiService CreateApiServiceWithSelectedDashboard(bool isPublicDashboardSettingAllowed = true)
        => new()
        {
            DashboardEntityOptionsResult =
            [
                new()
                {
                    EntityId = 1,
                    EntityName = "Orders",
                    Title = "Orders"
                }
            ],
            DashboardCatalogResult = new RgfDashboardCatalogResult
            {
                Dashboards = [new() { DashboardId = 7, Name = "Orders dashboard" }],
                SelectedDashboard = CreateSavedDashboard(7, "Orders dashboard"),
                IsPublicDashboardSettingAllowed = isPublicDashboardSettingAllowed
            },
            DashboardResult = CreateSavedDashboard(7, "Orders dashboard"),
            SaveDashboardResult = CreateSavedDashboard(7, "Orders dashboard")
        };

    private static RgfDashboardDefinition CreateSavedDashboard(int id, string name)
        => new()
        {
            DashboardId = id,
            Name = name,
            Layout = new()
            {
                RootPane = new()
                {
                    PaneId = "root",
                    DashboardItemId = 1
                },
                Items =
                [
                    new()
                    {
                        DashboardItemId = 1,
                        Title = "Orders",
                        ViewReference = new()
                        {
                            EntityName = "Orders",
                            ViewType = RgfDashboardViewType.Grid
                        }
                    }
                ]
            }
        };

    private static void ClickToolbarButton(IRenderedComponent<DashboardPageComponent> cut, string iconClass)
        => FindButton(cut, iconClass).Click();

    private static void ClickDesignerButton(IRenderedComponent<DashboardPageComponent> cut, string iconClass)
        => FindButton(cut, iconClass).Click();

    private static void ClickButtonByText(IRenderedComponent<DashboardPageComponent> cut, string text)
        => cut.FindAll("button").Single(button => button.TextContent.Trim() == text).Click();

    private static IElement FindLabeledControl(IRenderedComponent<DashboardPageComponent> cut, string labelText)
    {
        var label = cut.FindAll("label").Single(candidate => candidate.TextContent.Trim() == labelText);
        var elementId = label.GetAttribute("for");
        Assert.False(string.IsNullOrWhiteSpace(elementId));
        return cut.Find($"#{elementId}");
    }

    private static void SetInputValue(IRenderedComponent<DashboardPageComponent> cut, string labelText, string value)
        => FindLabeledControl(cut, labelText).Change(value);

    private static IElement GetDashboardSelector(IRenderedComponent<DashboardPageComponent> cut)
        => cut.Find("select");

    private static IElement FindDashboardRoleSelector(IRenderedComponent<DashboardPageComponent> cut)
        => cut.Find(".rgf-dashboard-designer-dashboard-fields select");

    private static IElement FindButton(IRenderedComponent<DashboardPageComponent> cut, string iconClass)
        => cut.FindAll("button").Single(button => button.QuerySelector($".{iconClass}") != null);

    private static void WaitForLoadedDashboard(IRenderedComponent<DashboardPageComponent> cut)
        => cut.WaitForAssertion(() => Assert.Contains("rgf-dashboard-runtime", cut.Markup));
}
