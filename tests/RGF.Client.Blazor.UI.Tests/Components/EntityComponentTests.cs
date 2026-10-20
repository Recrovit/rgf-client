using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class EntityComponentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceChart_LoadingSavedSettingsGeneratesOnceAfterParentRefresh(bool delayParentRefresh)
    {
        using var context = CreateTestContext(true);
        RgfBlazorConfiguration.RegisterEntityComponent<AggregateEntityHost>(string.Empty);
        var entity = CreateEntityParameters(RfgDisplayMode.Grid);
        entity.ChartParameters.SupportsWorkspace = true;
        var cut = RenderWorkspace(context, entity, standardChart: true);
        await cut.InvokeAsync(() => ChartCommand(entity));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        var api = (PendingRgfApiService)context.Services.GetRequiredService<IRgfApiService>();
        await entity.Manager!.GetFilterHandlerAsync();
        cut.FindAll("select").Single(select => select.QuerySelector("option[value='Card']") != null).Change("Card");
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "CreateChart").ClickAsync(new());
        Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus);
        Assert.Single(api.ChartRequests);

        await cut.InvokeAsync(() => chart.ChartSettingList.Add(new()
        {
            ChartSettingsId = 7, SettingsName = "Saved card", SeriesType = RgfChartSeriesType.Card,
            AggregationSettings = new() { Columns = [new() { Id = 0, Aggregate = "Count" }], Take = 5 },
            ParentGridSettings = new()
            {
                Conditions = [new() { Id = 1, QueryOperator = RgfFilter.QueryOperator.Equal, Param1 = "saved-filter" }],
                SQLTimeout = 67
            }
        }));
        cut.Render();
        var selection = cut.FindComponents<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base.RgfComboBox<int?>>()
            .Single(combo => combo.Instance.AllowCustom);
        var sourceChanges = 0;
        entity.Manager.ListHandler.AggregateSourceChanged += () => sourceChanges++;
        var forwardedChanges = new List<bool>();
        entity.ChartParameters.EventDispatcher.Subscribe(RgfChartEventKind.AggregateSourceChanged,
            args => { forwardedChanges.Add(args.Args.IsChartSettingsChange); });
        if (delayParentRefresh) api.GridResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var gridRequestsBefore = api.GridRequests.Count;
        var loading = cut.InvokeAsync(() => selection.Instance.OnSelected("7"));
        if (delayParentRefresh)
        {
            await api.GridResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            await cut.InvokeAsync(async () => await Task.Delay(150));
            Assert.False(loading.IsCompleted);
            Assert.Single(api.ChartRequests);
            Assert.Equal(RgfProcessingStatus.Invalid, chart.ChartStatus);
            Assert.Empty(cut.FindAll(".rgf-apexchart-card-view"));
            api.GridResponseGate!.SetResult(true);
        }
        await loading;
        await cut.InvokeAsync(async () => await Task.Delay(100));
        Assert.Equal(1, sourceChanges);
        Assert.Equal([true], forwardedChanges);
        Assert.Equal(gridRequestsBefore + 1, api.GridRequests.Count);
        Assert.Equal(2, api.ChartRequests.Count);
        Assert.Equal("saved-filter", Assert.Single(api.GridRequests[^1].ListParam.UserFilter).Param1?.ToString());
        Assert.Equal("saved-filter", Assert.Single(entity.Manager.ListHandler.GetGridSettings().Conditions!).Param1?.ToString());
        Assert.Equal("saved-filter", Assert.Single(api.ChartRequests[^1].ParentGridRequest.ListParam.UserFilter).Param1?.ToString());
        Assert.Equal(67, api.ChartRequests[^1].ParentGridRequest.ListParam.SQLTimeout);
        Assert.Equal(5, api.ChartRequests[^1].ParentGridRequest.ListParam.AggregationSettings.Take);
        Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus);
        Assert.Single(cut.FindAll(".rgf-apexchart-card-view"));

        await cut.InvokeAsync(() => entity.Manager.ListHandler.ApplyFilterState([new() { Id = 1, Param1 = "user-filter" }], 71));
        await api.WaitForChartRequestsAsync(3);
        cut.FindComponent<RgfChartComponent>().WaitForAssertion(() => Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus), TimeSpan.FromSeconds(5));
        Assert.Equal("user-filter", Assert.Single(api.ChartRequests[^1].ParentGridRequest.ListParam.UserFilter).Param1?.ToString());
        Assert.Equal(71, api.ChartRequests[^1].ParentGridRequest.ListParam.SQLTimeout);
        Assert.Equal([true, false], forwardedChanges);
        Assert.Single(cut.FindAll(".rgf-apexchart-card-view"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspaceChart_RefreshesLatestFilterAndHidesInvalidCards(bool failLatest)
    {
        using var context = CreateTestContext(true);
        RgfBlazorConfiguration.RegisterEntityComponent<AggregateEntityHost>(string.Empty);
        var entity = CreateEntityParameters(RfgDisplayMode.Grid);
        entity.ChartParameters.SupportsWorkspace = true;
        var cut = RenderWorkspace(context, entity, standardChart: true);
        await cut.InvokeAsync(() => ChartCommand(entity));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        var api = (PendingRgfApiService)context.Services.GetRequiredService<IRgfApiService>();
        var handler = entity.Manager!.ListHandler;
        await cut.InvokeAsync(() => handler.ApplyFilterState([new() { Id = 1, Param1 = "before-first-chart" }], 23));
        await cut.InvokeAsync(async () => await Task.Delay(100));
        Assert.Empty(api.ChartRequests);

        await cut.InvokeAsync(() =>
        {
            chart.ChartSettings.SettingsName = "Retained card";
        });
        cut.FindAll("select").Single(select => select.QuerySelector("option[value='Card']") != null).Change("Card");
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "CreateChart").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus), TimeSpan.FromSeconds(5));
        Assert.Single(cut.FindAll(".rgf-apexchart-card-view"));

        var gate = api.ChartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await cut.InvokeAsync(() => handler.ApplyFilterState([new() { Id = 1, Param1 = "first" }], 31));
        await api.WaitForChartRequestsAsync(2);
        Assert.Equal(RgfProcessingStatus.Invalid, chart.ChartStatus);
        Assert.Empty(cut.FindAll(".rgf-apexchart-card-view"));
        await cut.InvokeAsync(() =>
        {
            handler.ApplyFilterState([new() { Id = 1, Param1 = "second" }], 41);
            handler.ApplyFilterState([new() { Id = 1, Param1 = "latest" }], 47);
        });
        Assert.Equal(2, api.ChartRequests.Count);
        api.FailChartRequest = failLatest;
        api.ChartResponseGate = null;
        gate.SetResult(true);
        await api.WaitForChartRequestsAsync(3);
        cut.WaitForAssertion(() => Assert.Equal(failLatest ? RgfProcessingStatus.Invalid : RgfProcessingStatus.Valid, chart.DataStatus), TimeSpan.FromSeconds(5));
        Assert.Equal("latest", Assert.Single(api.ChartRequests[^1].ParentGridRequest.ListParam.UserFilter).Param1?.ToString());
        Assert.Equal(47, api.ChartRequests[^1].ParentGridRequest.ListParam.SQLTimeout);
        Assert.Equal(1, api.MaxConcurrentChartRequests);
        Assert.Equal("Retained card", chart.ChartSettings.SettingsName);
        Assert.Equal(RgfChartSeriesType.Card, chart.ChartSettings.SeriesType);
        if (failLatest)
        {
            Assert.Equal(RgfProcessingStatus.Invalid, chart.ChartStatus);
            Assert.Empty(cut.FindAll(".rgf-apexchart-card-view"));
        }
        else
        {
            cut.WaitForAssertion(() => Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus), TimeSpan.FromSeconds(5));
            Assert.Single(cut.FindAll(".rgf-apexchart-card-view"));
        }

        await cut.InvokeAsync(() => ChartCommand(entity));
        await cut.InvokeAsync(() => handler.ApplyFilterState([new() { Id = 1, Param1 = "hidden" }], 53));
        // Drain the deferred source notification before asserting that no query started.
        await cut.InvokeAsync(async () => await Task.Delay(100));
        Assert.Equal(3, api.ChartRequests.Count);
        Assert.Equal(RgfProcessingStatus.Invalid, chart.ChartStatus);
        await cut.InvokeAsync(() => ChartCommand(entity));
        await cut.InvokeAsync(async () => await Task.Delay(100));
        Assert.Equal(3, api.ChartRequests.Count);
        Assert.Equal(RgfProcessingStatus.Invalid, chart.ChartStatus);
    }

    private sealed class AggregateEntityHost : ComponentBase
    {
        [Inject] public IServiceProvider Services { get; set; } = null!;
        [Parameter] public RgfEntityParameters EntityParameters { get; set; } = null!;
        protected override void OnInitialized()
        {
            var manager = new RgManager(EntityParameters, Services);
            manager.RefreshEntity += _ => { };
            typeof(RgfEntityParameters).GetProperty(nameof(RgfEntityParameters.Manager))!.SetValue(EntityParameters, manager);
        }
    }

    [Theory]
    [InlineData(false, false, RfgDisplayMode.Grid)]
    [InlineData(false, false, RfgDisplayMode.Tree)]
    [InlineData(false, false, RfgDisplayMode.Chart)]
    [InlineData(false, true, RfgDisplayMode.Grid)]
    [InlineData(true, false, RfgDisplayMode.Grid)]
    [InlineData(true, true, RfgDisplayMode.Grid)]
    public async Task RegisteredChart_SelectsPresentationAndPreservesLifecycle(bool apexOnly, bool embedded, RfgDisplayMode initialMode)
    {
        using var context = CreateTestContext(true);
        await context.Services.InitializeRgfUIAsync(loadResources: false);
        if (apexOnly)
            await Recrovit.RecroGridFramework.Blazor.RgfApexCharts.RgfApexChartsConfigurationExtension.InitializeRGFBlazorApexChartsAsync(context.Services, loadResources: false);
        // Keep the chart DataSet lightweight; the entity below still uses the real UI wrapper and chart registration.
        RgfBlazorConfiguration.RegisterEntityComponent<StatefulEntity>(string.Empty);
        var entity = CreateEntityParameters(initialMode);
        entity.IsEmbeddedEntity = embedded;
        Assert.False(entity.ChartParameters.SupportsWorkspace);
        var cut = context.Render<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.EntityComponent>(parameters => parameters
            .Add(component => component.EntityParameters, entity)
            .Add(component => component.GridTemplate, _ => builder => { builder.OpenComponent<StatefulView>(0); builder.CloseComponent(); })
            .Add(component => component.TreeTemplate, _ => builder => builder.AddMarkupContent(0, "<div class='tree'>Tree</div>"))
            .Add(component => component.ToolbarTemplate, _ => builder => { })
            .Add(component => component.FilterTemplate, _ => builder => { })
            .Add(component => component.PagerTemplate, _ => builder => { })
            .Add(component => component.FormTemplate, _ => builder => { }));
        var grid = cut.FindComponent<StatefulView>().Instance;
        if (initialMode != RfgDisplayMode.Chart)
            await cut.InvokeAsync(() => ChartCommand(entity));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<RgfChartComponent>()));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        var workspace = !apexOnly && !embedded;
        Assert.Equal(workspace ? RgfChartPresentationMode.Workspace : RgfChartPresentationMode.Dialog, entity.ChartParameters.PresentationMode);
        Assert.Equal(workspace, chart.IsWorkspace);
        if (workspace)
        {
            Assert.DoesNotContain(">Close<", cut.Markup);
            await cut.InvokeAsync(() =>
            {
                chart.ChartSettings.SettingsName = "Retained";
                chart.SetDataStatus(RgfProcessingStatus.Valid);
            });
            await cut.InvokeAsync(() => ChartCommand(entity));
            Assert.Equal(initialMode == RfgDisplayMode.Chart ? RfgDisplayMode.Grid : initialMode, entity.DisplayMode);
            await cut.InvokeAsync(() => ChartCommand(entity));
            Assert.Same(chart, cut.FindComponent<RgfChartComponent>().Instance);
            Assert.Same(grid, cut.FindComponent<StatefulView>().Instance);
            Assert.Equal("Retained", chart.ChartSettings.SettingsName);
            Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
            var ui = cut.FindComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ChartComponent>();
            cut.FindAll("button").Single(button => button.TextContent.Trim() == "Chart").Click();
            await cut.InvokeAsync(() => ui.Instance.ResizeCommit(800, 600));
            Assert.Null(chart.ChartSettings.Width);
            Assert.Null(chart.ChartSettings.Height);
            chart.ChartSettings.Width = 420;
            chart.ChartSettings.Height = 300;
            await cut.InvokeAsync(() => ui.Instance.ResizeCommit(900, 700));
            Assert.Equal(420, chart.ChartSettings.Width);
            Assert.Equal(300, chart.ChartSettings.Height);
            await entity.Manager!.GetFilterHandlerAsync();
            var data = chart.ChartData;
            var requests = 0;
            entity.Manager!.CreateGridRequestCreated += (_, _) => requests++;
            await cut.InvokeAsync(() => ChartCommand(entity));
            await cut.InvokeAsync(() => ChartCommand(entity));
            Assert.Contains("active", cut.FindAll("button").Single(button => button.TextContent.Trim() == "Chart").ClassList);
            Assert.Same(data, chart.ChartData);
            Assert.Equal(0, requests);

            // Saved settings refresh the filtered parent before returning to the caller's chart load.
            var api = (PendingRgfApiService)context.Services.GetRequiredService<IRgfApiService>();
            api.GridResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            chart.ChartSettingList.Add(new RgfChartSettings
            {
                ChartSettingsId = 7, SettingsName = "Saved",
                ParentGridSettings = new RgfGridSettings
                {
                    Conditions = [new() { Id = 1, QueryOperator = RgfFilter.QueryOperator.Equal, Param1 = "saved-filter" }]
                }
            });
            var loading = cut.InvokeAsync(async () => { Assert.True(await chart.OnSetChartSettingAsync(7, "Saved")); });
            if (loading.IsCompleted) await loading;
            cut.WaitForAssertion(() => Assert.Equal(1, requests));
            if (loading.IsCompleted) await loading;
            Assert.False(loading.IsCompleted);
            Assert.Equal("saved-filter", Assert.Single(entity.Manager.ListHandler.GetGridSettings().Conditions!).Param1?.ToString());
            api.GridResponseGate.SetResult(true);
            await loading;
            Assert.False(chart.ConsumePendingParentRefresh());
            Assert.Equal("Saved", chart.ChartSettings.SettingsName);
            await cut.InvokeAsync(() => chart.SetDataStatus(RgfProcessingStatus.Valid));
            await cut.InvokeAsync(() => entity.Manager.ListHandler.RefreshRowAsync(new RgfDynamicDictionary()));
            Assert.Equal(RgfProcessingStatus.Invalid, chart.DataStatus);
        }
        else
        {
            Assert.Contains("Close", cut.Markup);
            await cut.InvokeAsync(() => chart.OnClose(null));
            await cut.InvokeAsync(() => ChartCommand(entity));
            Assert.Equal(initialMode, entity.DisplayMode);
            Assert.DoesNotContain(context.JSInterop.Invocations, call => call.Identifier == "import" &&
                call.Arguments.Any(argument => argument?.ToString()?.Contains("ChartWorkspace.js") == true));
        }
    }

    [Fact]
    public async Task CustomChartTemplate_WaitsForChartResourcesBeforeRenderingDialog()
    {
        using var context = CreateTestContext(true);
        var dictionary = new FakeRecroDictService();
        context.Services.AddSingleton<IRecroDictService>(dictionary);
        var entity = CreateEntityParameters(RfgDisplayMode.Grid);
        var cut = RenderWorkspace(context, entity, standardChart: true);
        await cut.InvokeAsync(() => ChartCommand(entity));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<RgfChartComponent>()));
        Assert.Empty(cut.FindAll(".rgf-chart-workspace"));
        dictionary.ChartReady.SetResult(true);
        cut.WaitForAssertion(() => Assert.Contains("CreateChart", cut.Markup));
        Assert.Equal(RgfChartPresentationMode.Dialog, entity.ChartParameters.PresentationMode);
        Assert.Equal(RfgDisplayMode.Grid, entity.DisplayMode);
    }

    [Fact]
    public async Task DeferredEmbeddedEntity_InitializesVisibleGridAfterManagerRecreation()
    {
        using var context = CreateTestContext(true);
        var entity = new RgfEntityParameters("RGRecroChart") { IsEmbeddedEntity = true, DeferredInitialization = true };
        var cut = RenderWorkspace(context, entity);
        Assert.Empty(cut.FindAll(".rgf-entity-grid"));
        await cut.InvokeAsync(() => entity.Manager!.RecreateAsync());
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<StatefulView>()));
        Assert.Equal(RfgDisplayMode.Grid, entity.DisplayMode);
        Assert.Equal(RgfChartPresentationMode.Dialog, entity.ChartParameters.PresentationMode);
        Assert.DoesNotContain("display:none", cut.Find(".rgf-entity-grid").GetAttribute("style") ?? "");
    }

    [Theory]
    [InlineData(RfgDisplayMode.Grid)]
    [InlineData(RfgDisplayMode.Tree)]
    [InlineData(RfgDisplayMode.Chart)]
    public async Task StandardChart_UsesDialogWithCloseAndResize_AndReopensFromMenu(RfgDisplayMode initialMode)
    {
        using var context = CreateTestContext(true);
        var entity = CreateEntityParameters(initialMode);
        var cut = RenderWorkspace(context, entity, standardChart: true);
        await cut.InvokeAsync(() => ChartCommand(entity));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<RgfChartComponent>()));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        Assert.False(chart.IsWorkspace);
        Assert.Equal(RgfChartPresentationMode.Dialog, entity.ChartParameters.PresentationMode);
        var expectedMode = initialMode == RfgDisplayMode.Chart ? RfgDisplayMode.Grid : initialMode;
        Assert.Equal(expectedMode, entity.DisplayMode);
        Assert.Single(cut.FindAll(".modal"));
        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Close");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Chart").Click();
        var chartUi = cut.FindComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ChartComponent>();
        await cut.InvokeAsync(() => chartUi.Instance.ResizeCommit(700, 500));
        Assert.Equal(698, chart.ChartSettings.Width);
        Assert.Equal(440, chart.ChartSettings.Height);
        await cut.InvokeAsync(() => chart.OnClose(null));
        Assert.Empty(cut.FindAll(".modal"));
        await cut.InvokeAsync(() => ChartCommand(entity, menu: true));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".modal")));
        Assert.Same(chart, cut.FindComponent<RgfChartComponent>().Instance);
        Assert.Equal(expectedMode, entity.DisplayMode);
        Assert.DoesNotContain(context.JSInterop.Invocations, invocation => invocation.Identifier == "import" &&
            invocation.Arguments.Any(argument => argument?.ToString()?.EndsWith("BaseChartComponent.js") == true));
    }

    [Fact]
    public async Task Workspace_InvalidatesDataOnlyWhenSourceChanges_AndFreezesPresentation()
    {
        using var context = CreateTestContext(true);
        var entity = CreateEntityParameters(RfgDisplayMode.Grid);
        var cut = RenderWorkspace(context, entity);
        await cut.InvokeAsync(() => ChartCommand(entity));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        await cut.InvokeAsync(() => chart.SetDataStatus(RgfProcessingStatus.Valid));
        await cut.InvokeAsync(() => ChartCommand(entity));
        entity.IsEmbeddedEntity = true;
        cut.Render();
        Assert.Equal(RgfChartPresentationMode.Workspace, entity.ChartParameters.PresentationMode);
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
        var handler = entity.Manager!.ListHandler;
        await cut.InvokeAsync(() => handler.ListDataSource.SetValueAsync([new RgfDynamicDictionary()]));
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
        await cut.InvokeAsync(() => handler.ActivePage.SetValueAsync(2));
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
        var pager = context.Render<RgfPagerComponent>(parameters => parameters
            .Add(component => component.EntityParameters, entity)
            .Add(component => component.PagerTemplate, _ => builder => { }));
        await cut.InvokeAsync(() => pager.Instance.PageSizeChanging(50));
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);

        var condition = new RgfFilter.Condition { Id = 1, QueryOperator = RgfFilter.QueryOperator.Equal, Param1 = "A" };
        await cut.InvokeAsync(() => handler.SetFilterAsync([condition], null));
        Assert.Equal(RgfProcessingStatus.Invalid, chart.DataStatus);
        await cut.InvokeAsync(() => chart.SetDataStatus(RgfProcessingStatus.Valid));
        await cut.InvokeAsync(() => handler.SetFilterAsync([condition], null));
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
        condition.Param1 = "B";
        await cut.InvokeAsync(() => handler.ApplyFilterState([condition], null));
        Assert.Equal(RgfProcessingStatus.Invalid, chart.DataStatus);
        await cut.InvokeAsync(() => chart.SetDataStatus(RgfProcessingStatus.Valid));
        await cut.InvokeAsync(() => handler.SetFilterAsync([], null));
        Assert.Equal(RgfProcessingStatus.Invalid, chart.DataStatus);
        await cut.InvokeAsync(() => chart.SetDataStatus(RgfProcessingStatus.Valid));
        await cut.InvokeAsync(() => handler.RefreshDataAsync());
        Assert.Equal(RgfProcessingStatus.Invalid, chart.DataStatus);
    }

    [Theory]
    [InlineData(RfgDisplayMode.Grid)]
    [InlineData(RfgDisplayMode.Tree)]
    [InlineData(RfgDisplayMode.Chart)]
    public async Task Workspace_PreservesInstancesAndState_ThroughToolbarAndMenuSwitches(RfgDisplayMode initialMode)
    {
        using var context = CreateTestContext(true);
        var entity = CreateEntityParameters(initialMode);
        var cut = RenderWorkspace(context, entity);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".toolbar")));
        var grid = cut.FindComponent<StatefulView>().Instance;
        var manager = entity.Manager;
        if (initialMode != RfgDisplayMode.Chart)
        {
            await cut.InvokeAsync(() => ChartCommand(entity));
        }
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<RgfChartComponent>()));
        var chart = cut.FindComponent<RgfChartComponent>().Instance;
        var editor = cut.FindComponents<StatefulView>().Last();
        editor.Find("input").Change("retained tab and draft");
        await cut.InvokeAsync(() =>
        {
            chart.ChartSettings.SettingsName = "Retained chart";
            chart.SetDataStatus(RgfProcessingStatus.Valid);
            chart.ChartStatus = RgfProcessingStatus.Valid;
        });
        Assert.Equal(RfgDisplayMode.Chart, entity.DisplayMode);
        Assert.Empty(cut.FindAll(".pager"));
        Assert.Single(cut.FindAll(".toolbar"));
        Assert.Single(cut.FindAll(".filter"));
        Assert.Empty(cut.FindAll(".modal"));
        Assert.Contains("display:none", cut.Find(".rgf-entity-grid").GetAttribute("style"));

        await cut.InvokeAsync(() => ChartCommand(entity, menu: true));
        Assert.Equal(initialMode == RfgDisplayMode.Tree ? RfgDisplayMode.Tree : RfgDisplayMode.Grid, entity.DisplayMode);
        await cut.InvokeAsync(() => ChartCommand(entity));

        Assert.Same(grid, cut.FindComponents<StatefulView>().First().Instance);
        Assert.Same(manager, entity.Manager);
        Assert.Same(chart, cut.FindComponent<RgfChartComponent>().Instance);
        Assert.Equal("Retained chart", chart.ChartSettings.SettingsName);
        Assert.Equal(RgfProcessingStatus.Valid, chart.DataStatus);
        Assert.Equal(RgfProcessingStatus.Valid, chart.ChartStatus);
        Assert.Equal("retained tab and draft", cut.FindComponents<StatefulView>().Last().Find("input").GetAttribute("value"));
    }

    [Theory]
    [InlineData(RfgDisplayMode.Grid)]
    [InlineData(RfgDisplayMode.Tree)]
    [InlineData(RfgDisplayMode.Chart)]
    public async Task EmbeddedChart_UsesDialogAndKeepsDisplayMode(RfgDisplayMode initialMode)
    {
        using var context = CreateTestContext(true);
        var entity = CreateEntityParameters(initialMode);
        entity.IsEmbeddedEntity = true;
        entity.DialogTemplate = settings => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "modal");
            builder.AddContent(2, settings.ContentTemplate);
            builder.CloseElement();
        };
        var cut = RenderWorkspace(context, entity);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".toolbar")));
        var expectedMode = initialMode == RfgDisplayMode.Chart ? RfgDisplayMode.Grid : initialMode;
        await cut.InvokeAsync(() => ChartCommand(entity));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".modal")));
        var chart = cut.FindComponent<RgfChartComponent>();
        Assert.Equal(RgfChartPresentationMode.Dialog, entity.ChartParameters.PresentationMode);
        Assert.Equal(expectedMode, entity.DisplayMode);
        await cut.InvokeAsync(() => chart.Instance.OnClose(null));
        Assert.Empty(cut.FindAll(".modal"));
        await cut.InvokeAsync(() => ChartCommand(entity, menu: true));
        Assert.Single(cut.FindAll(".modal"));
        Assert.Equal(expectedMode, entity.DisplayMode);
    }

    private static Task<bool> ChartCommand(RgfEntityParameters entity, bool menu = false)
        => menu
            ? entity.ToolbarParameters.MenuEventDispatcher.DispatchEventAsync(Recrovit.RecroGridFramework.Abstraction.Contracts.Constants.Menu.RecroChart,
                new RgfEventArgs<RgfMenuEventArgs>(entity, new(Recrovit.RecroGridFramework.Abstraction.Contracts.Constants.Menu.RecroChart, "RecroChart")))
            : entity.ToolbarParameters.EventDispatcher.DispatchEventAsync(RgfToolbarEventKind.RecroChart,
                new RgfEventArgs<RgfToolbarEventArgs>(entity, new(RgfToolbarEventKind.RecroChart)));

    private static IRenderedComponent<RgfEntityComponent> RenderWorkspace(BunitContext context, RgfEntityParameters entity, bool standardChart = false)
    {
        entity.ChartParameters.SupportsWorkspace |= !standardChart;
        return context.Render<RgfEntityComponent>(parameters => parameters
            .Add(component => component.EntityParameters, entity)
            .Add(component => component.GridTemplate, _ => builder => { builder.OpenComponent<StatefulView>(0); builder.CloseComponent(); })
            .Add(component => component.TreeTemplate, _ => builder => builder.AddMarkupContent(0, "<div class='tree'>Tree</div>"))
            .Add(component => component.ToolbarTemplate, _ => builder => builder.AddMarkupContent(0, "<div class='toolbar'>Toolbar</div>"))
            .Add(component => component.FilterTemplate, _ => builder => builder.AddMarkupContent(0, "<div class='filter'>Filter</div>"))
            .Add(component => component.PagerTemplate, _ => builder => builder.AddMarkupContent(0, "<div class='pager'>Pager</div>"))
            .Add(component => component.FormTemplate, _ => builder => { })
            .Add(component => component.ChartTemplate, current => builder =>
            {
                if (standardChart)
                {
                    builder.OpenComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.ChartComponent>(0);
                    builder.AddAttribute(1, "EntityParameters", current);
                    builder.CloseComponent();
                    return;
                }
                builder.OpenComponent<RgfChartComponent>(0);
                builder.AddAttribute(1, nameof(RgfChartComponent.EntityParameters), current);
                builder.AddAttribute(2, nameof(RgfChartComponent.ContentTemplate), (RenderFragment<RgfChartComponent>)(_ => content =>
                {
                    content.OpenComponent<StatefulView>(0);
                    content.CloseComponent();
                }));
                builder.AddAttribute(3, nameof(RgfChartComponent.FooterTemplate), (RenderFragment<RgfChartComponent>)(_ => content => content.AddContent(0, "Chart footer")));
                builder.CloseComponent();
            }));
    }

    private sealed class StatefulView : ComponentBase
    {
        private string _draft = "";
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "value", _draft);
            builder.AddAttribute(2, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => _draft = e.Value?.ToString() ?? ""));
            builder.CloseElement();
        }
    }

    [Fact]
    public void PassesTreeLoadingIndicatorStyleToBaseComponent()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(RfgDisplayMode.Tree);

        var cut = testContext.Render<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.EntityComponent>(parameters =>
            parameters.Add(component => component.EntityParameters, entityParameters));

        var baseComponent = cut.FindComponent<RgfEntityComponent>();

        Assert.Equal("width: 1rem; height: 1rem;", baseComponent.Instance.LoadingIndicatorParameters?.Style);
    }

    [Fact]
    public void PassesGridLoadingIndicatorStyleToBaseComponent()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(RfgDisplayMode.Grid);

        var cut = testContext.Render<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.EntityComponent>(parameters =>
            parameters.Add(component => component.EntityParameters, entityParameters));

        var baseComponent = cut.FindComponent<RgfEntityComponent>();

        Assert.Equal("width: 3rem; height: 3rem;", baseComponent.Instance.LoadingIndicatorParameters?.Style);
    }

    private static BunitContext CreateTestContext(bool complete = false)
    {
        RgfClientBlazorUiTestState.Reset();

        var testContext = new BunitContext();
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService>(new FakeRecroSecService("eng"));
        testContext.Services.AddSingleton<IRgfEventNotificationService, FakeEventNotificationService>();
        testContext.Services.AddSingleton<IRgfApiService>(new PendingRgfApiService(complete));
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        RgfBlazorConfiguration.RegisterComponent<Recrovit.RecroGridFramework.Client.Blazor.UI.Components.DialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
        RgfBlazorConfiguration.RegisterEntityComponent<StatefulEntity>(string.Empty);
        return testContext;
    }

    private static RgfEntityParameters CreateEntityParameters(RfgDisplayMode displayMode)
    {
        var sessionParams = new RgfSessionParams
        {
            GridId = "1",
            SessionId = Guid.NewGuid().ToString("N")
        };

        return new RgfEntityParameters("Orders", sessionParams)
        {
            DisplayMode = displayMode
        };
    }

    private sealed class StatefulEntity : ComponentBase
    {
        [Parameter] public RgfEntityParameters EntityParameters { get; set; } = null!;
    }

    private sealed class PendingRgfApiService(bool complete) : IRgfApiService
    {
        public TaskCompletionSource<bool>? GridResponseGate { get; set; }
        public TaskCompletionSource GridResponseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<RgfGridRequest> GridRequests { get; } = [];
        public TaskCompletionSource<bool>? ChartResponseGate { get; set; }
        public List<RgfGridRequest> ChartRequests { get; } = [];
        public bool FailChartRequest { get; set; }
        private int _activeChartRequests;
        public int MaxConcurrentChartRequests { get; private set; }
        private TaskCompletionSource _chartRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitForChartRequestsAsync(int count)
        {
            while (ChartRequests.Count < count)
                await _chartRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(count, ChartRequests.Count);
        }

        private async Task<IRgfApiResponse<T>> ChartResponse<T>(RgfGridRequest request) where T : class
        {
            ChartRequests.Add(request);
            var started = _chartRequestStarted;
            _chartRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            started.SetResult();
            MaxConcurrentChartRequests = Math.Max(MaxConcurrentChartRequests, ++_activeChartRequests);
            var fail = FailChartRequest;
            var value = ChartRequests.Count;
            try
            {
                if (ChartResponseGate != null) await ChartResponseGate.Task;
                return new ApiResponse<T>
                {
                    Success = true,
                    Result = (T)(object)new RgfResult<RgfGridResult>
                    {
                        Success = !fail, Messages = new(),
                        Result = new()
                        {
                            EntityDesc = new RgfEntity
                            {
                                EntityName = "RGRecroChart", EntityVersion = "1", Permissions = new(true), Options = [],
                                Properties = [new() { Id = 1, Alias = "Count", ClientName = "Count", ColTitle = "Count", Readable = true, ColPos = 1, FormType = PropertyFormType.TextBox, ListType = PropertyListType.Numeric }]
                            },
                            DataColumns = ["Count"], Data = [[value]], Options = new() { ["RGO_MaxItem"] = 1 }
                        }
                    }
                };
            }
            finally { _activeChartRequests--; }
        }

        private async Task<IRgfApiResponse<T>> WaitForGridResponse<T>(IRgfApiResponse<T> response) where T : class
        {
            if (GridResponseGate != null)
            {
                GridResponseStarted.TrySetResult();
                await GridResponseGate.Task;
            }
            return response;
        }
        public Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request) where ResultType : class
        {
            object result = typeof(ResultType) == typeof(RgfResult<Dictionary<string, string>>)
                ? new RgfResult<Dictionary<string, string>> { Success = true, Messages = new(), Result = [] }
                : typeof(ResultType) == typeof(RgfResult<List<RgfChartSettings>>)
                ? new RgfResult<List<RgfChartSettings>> { Success = true, Messages = new(), Result = [] }
                : throw new NotSupportedException($"Unsupported GET {typeof(ResultType)}");
            return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType> { Success = true, Result = (ResultType)result });
        }

        public Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request) where ResultType : class
        {
            if (typeof(ResultType) == typeof(RgfResult<RgfFilterResult>))
            {
                return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
                {
                    Success = true,
                    Result = (ResultType)(object)new RgfResult<RgfFilterResult>
                    {
                        Success = true, Messages = new(), Result = new() { XmlFilter = "<columns />", FilterSettings = [] }
                    }
                });
            }
            if (typeof(ResultType) == typeof(RgfResult<List<RgfChartSettings>>))
            {
                return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
                {
                    Success = true, Result = (ResultType)(object)new RgfResult<List<RgfChartSettings>> { Success = true, Messages = new(), Result = [] }
                });
            }
            if (typeof(ResultType) == typeof(RgfResult<RgfGridResult>) &&
                string.Equals(request.Uri, "/rgf/api/entity/RecroGrid", StringComparison.Ordinal))
            {
                var gridRequest = JsonSerializer.Deserialize<RgfGridRequest>(request.Content.ReadAsStringAsync().GetAwaiter().GetResult(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                if (gridRequest.EntityName == "RGRecroChart") return ChartResponse<ResultType>(gridRequest);
                GridRequests.Add(gridRequest);
                if (complete)
                {
                    return WaitForGridResponse<ResultType>(new ApiResponse<ResultType>
                    {
                        Success = true,
                        Result = (ResultType)(object)new RgfResult<RgfGridResult>
                        {
                            Success = true,
                            Messages = new(),
                            Result = new RgfGridResult
                            {
                                EntityDesc = new RgfEntity { EntityName = "Orders", EntityVersion = "1", MenuTitle = "Orders", Title = "Orders", Permissions = new RgfPermissions(true), CRUD = "CRUD", Properties = [], Options = gridRequest.ListParam?.SQLTimeout is int timeout ? new() { ["RGO_SQLTimeout"] = timeout } : [] },
                                Data = [], DataColumns = [], Options = new() { ["RGO_MaxItem"] = 0 }
                            }
                        }
                    });
                }
                return new TaskCompletionSource<IRgfApiResponse<ResultType>>().Task;
            }

            throw new NotSupportedException($"Unsupported POST result type '{typeof(ResultType)}' for '{request.Uri}'.");
        }
    }

    private sealed class FakeEventNotificationService : IRgfEventNotificationService
    {
        public IRgfNotificationManager GetNotificationManager(string scope) => new FakeNotificationManager();

        public bool RemoveNotificationManager(string scope) => true;
    }

    private sealed class FakeNotificationManager : IRgfNotificationManager
    {
        public IRgfObservableEvent<TArgs> GetObservableEvents<TArgs>() where TArgs : EventArgs => throw new NotSupportedException();
        public Task RaiseEventAsync<TArgs>(TArgs args, object sender) where TArgs : EventArgs => Task.CompletedTask;
        public IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Action<IRgfEventArgs<TArgs>> handler) where TArgs : EventArgs => new FakeObserver<IRgfEventArgs<TArgs>>();
        public IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Func<IRgfEventArgs<TArgs>, Task> handler) where TArgs : EventArgs => new FakeObserver<IRgfEventArgs<TArgs>>();
        public void Dispose() { }
    }

    private sealed class FakeObserver<TValue> : IRgfObserver<TValue>
    {
        public void Dispose() { }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(TValue value) { }
        public Task OnNextAsync(TValue value) => Task.CompletedTask;
        public void Unsubscribe() { }
    }

    private sealed class FakeRecroDictService : IRecroDictService
    {
        public TaskCompletionSource<bool> ChartReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly FakeDashboardRecroDictService _resources = new();
        public bool IsInitialized => true;
        public Dictionary<string, string> Languages { get; } = [];
        public string DefaultLanguage => "eng";
        public Task InitializeAsync(string language = null!) => Task.CompletedTask;
        public async Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
        {
            if (scope == "RGF.UI.Chart") await ChartReady.Task;
            return await _resources.GetDictionaryAsync(scope, language, authClient);
        }
        public string GetRgfUiString(string resourceKey) => resourceKey;
        public string GetRgfUiString(string resourceKey, params object[] args) => resourceKey;
    }

    private sealed class FakeRecroSecService(string userLanguage) : IRecroSecService
    {
        public EventDispatcher<EventArgs> AuthenticationStateChanged { get; } = new();
        public EventDispatcher<DataEventArgs<RgfUserState>> UserStateChangedEvent { get; } = new();
        public string? UserName => null;
        public bool IsAuthenticated => false;
        public bool IsAdmin => false;
        public List<string> RoleClaim { get; } = [];
        public ClaimsPrincipal CurrentUser { get; } = new(new ClaimsIdentity());
        public RgfUserState UserState { get; } = new();
        public IReadOnlyDictionary<string, string> Roles { get; } = new Dictionary<string, string>();
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
        public string UserLanguage => userLanguage;
        public Task<string?> SetUserLanguageAsync(string? language) => Task.FromResult(language);
        public EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; } = new();
        public Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings) => Task.FromResult(false);
        public Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60) => Task.FromResult(new RgfPermissions(true));
        public Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60) => Task.FromResult(new RgfPermissions(true));
        public Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60) => Task.FromResult(new List<RecroSecResult>());
    }
}
