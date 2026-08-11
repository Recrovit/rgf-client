using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components.Dashboard;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class RgfDashboardPaneComponentTests : IDisposable
{
    public RgfDashboardPaneComponentTests()
    {
        ResetStaticState();
    }

    public void Dispose()
    {
        ResetStaticState();
    }

    [Fact]
    public void Header_UsesSettingsName_WhenTitleIsBlank_AndHeaderIsVisible()
    {
        using var testContext = CreateTestContext();
        var item = CreateItem(title: "   ", showHeader: true, settingsName: "Orders default");
        var cut = RenderPane(testContext, item);

        var header = cut.Find(".rgf-dashboard-pane-header");
        Assert.Contains("Orders default", header.TextContent);
        Assert.DoesNotContain("Orders title", header.TextContent);
    }

    [Fact]
    public void Header_IsHidden_WhenShowHeaderIsFalse_EvenIfSettingsNameExists()
    {
        using var testContext = CreateTestContext();
        var item = CreateItem(title: null, showHeader: false, settingsName: "Orders default");
        var cut = RenderPane(testContext, item);

        Assert.Empty(cut.FindAll(".rgf-dashboard-pane-header"));
    }

    private static void ResetStaticState()
    {
        RgfClientBlazorUiTestState.Reset();
        FakeEntityHostComponent.Reset();
    }

    private static BunitContext CreateTestContext()
    {
        var testContext = new BunitContext();
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        RgfBlazorConfiguration.RegisterComponent<FakeLoadingComponent>(RgfBlazorConfiguration.ComponentType.LoadingIndicator);
        RgfBlazorConfiguration.RegisterComponent<FakeChartComponent>(RgfBlazorConfiguration.ComponentType.Chart);
        RgfBlazorConfiguration.RegisterEntityComponent<FakeEntityHostComponent>(string.Empty);
        return testContext;
    }

    private static IRenderedComponent<RgfDashboardPaneComponent> RenderPane(BunitContext testContext, RgfDashboardItem item)
        => testContext.Render<RgfDashboardPaneComponent>(parameters => parameters
            .Add(component => component.Pane, new RgfDashboardPane
            {
                PaneId = "root",
                DashboardItemId = item.DashboardItemId
            })
            .Add(component => component.ItemIndex, new Dictionary<int, RgfDashboardItem>
            {
                [item.DashboardItemId] = item
            })
            .Add(component => component.LayoutCommitVersion, 0));

    private static RgfDashboardItem CreateItem(string? title, bool showHeader, string? settingsName)
        => new()
        {
            DashboardItemId = 1,
            Title = title,
            ShowHeader = showHeader,
            ViewReference = new()
            {
                EntityName = "Orders",
                ViewType = RgfDashboardViewType.Grid,
                SettingsId = 42,
                SettingsName = settingsName ?? string.Empty
            }
        };

    private sealed class FakeLoadingComponent : ComponentBase
    {
        [Parameter]
        public RgfLoadingIndicatorParameters LoadingIndicatorParameters { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "dashboard-loading-component");
            builder.AddContent(2, LoadingIndicatorParameters.Status ?? LoadingIndicatorParameters.Text ?? "loading");
            builder.CloseElement();
        }
    }

    private sealed class FakeChartComponent : ComponentBase
    {
        [Parameter]
        public RgfEntityParameters? EntityParameters { get; set; }

        [Parameter]
        public bool Embedded { get; set; }

        [Parameter]
        public int? InitialChartSettingsId { get; set; }

        [Parameter]
        public bool HideChartControls { get; set; }

        [Parameter]
        public bool DataOnly { get; set; }

        [Parameter]
        public string? ResizeHostId { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fake-chart-component");
            builder.AddContent(2, $"{EntityParameters?.EntityName}:{InitialChartSettingsId}:{DataOnly}");
            builder.CloseElement();
        }
    }

    private sealed class FakeEntityHostComponent : ComponentBase
    {
        private static RgfEntityParameters? PendingInitialization;

        [Parameter]
        public RgfEntityParameters EntityParameters { get; set; } = null!;

        protected override void OnParametersSet()
        {
            PendingInitialization = EntityParameters;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fake-entity-host");
            builder.AddContent(2, EntityParameters.EntityName);
            builder.CloseElement();
        }

        public static void Reset()
        {
            PendingInitialization = null;
        }
    }

    private sealed class FakeRecroSecService : IRecroSecService
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
        public string UserLanguage => "eng";
        public EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; } = new();
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
        public Task<string?> SetUserLanguageAsync(string? language) => Task.FromResult(language);
        public Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings) => Task.FromResult(false);
        public Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60) => Task.FromResult(new RgfPermissions(true));
        public Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60) => Task.FromResult(new RgfPermissions(true));
        public Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60) => Task.FromResult(new List<RecroSecResult>());
    }
}
