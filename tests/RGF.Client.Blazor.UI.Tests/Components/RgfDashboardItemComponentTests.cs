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
public sealed class RgfDashboardItemComponentTests : IDisposable
{
    public RgfDashboardItemComponentTests()
    {
        ResetStaticState();
    }

    public void Dispose()
    {
        ResetStaticState();
    }

    [Fact]
    public void GridPanel_ShowsRegisteredLoadingIndicator_BeforeEntityInitialization_AndHidesItAfterward()
    {
        using var testContext = CreateTestContext();
        var cut = RenderComponent(testContext, CreateItem(RgfDashboardViewType.Grid, settingsId: 1));

        Assert.Contains("dashboard-loading-component", cut.Markup);
        Assert.Contains("Loading", cut.Markup);
        Assert.Contains("fake-entity-host", cut.Markup);

        FakeEntityHostComponent.DispatchNextInitialized();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("dashboard-loading-component", cut.Markup);
            Assert.Contains("fake-entity-host", cut.Markup);
        });
    }

    [Fact]
    public void ChartPanel_KeepsLoadingIndicatorVisible_UntilChartBecomesRenderable()
    {
        using var testContext = CreateTestContext();
        var cut = RenderComponent(testContext, CreateItem(RgfDashboardViewType.Chart, settingsId: 2));

        Assert.Contains("dashboard-loading-component", cut.Markup);
        Assert.DoesNotContain("fake-chart-component", cut.Markup);

        FakeEntityHostComponent.DispatchNextInitialized();

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("dashboard-loading-component", cut.Markup);
            Assert.Contains("fake-chart-component", cut.Markup);
        });
    }

    [Fact]
    public async Task PanelReinitialization_ShowsLoadingIndicatorAgain_WhenViewSettingsChange()
    {
        using var testContext = CreateTestContext();
        var cut = RenderComponent(testContext, CreateItem(RgfDashboardViewType.Grid, settingsId: 1));

        FakeEntityHostComponent.DispatchNextInitialized();
        cut.WaitForAssertion(() => Assert.DoesNotContain("dashboard-loading-component", cut.Markup));

        await cut.InvokeAsync(() => cut.Instance.SetItem(CreateItem(RgfDashboardViewType.Grid, settingsId: 99)));

        Assert.Contains("dashboard-loading-component", cut.Markup);

        FakeEntityHostComponent.DispatchNextInitialized();
        cut.WaitForAssertion(() => Assert.DoesNotContain("dashboard-loading-component", cut.Markup));
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

    private static IRenderedComponent<DashboardItemHostComponent> RenderComponent(BunitContext testContext, RgfDashboardItem item)
        => testContext.Render<DashboardItemHostComponent>(parameters => parameters
            .Add(component => component.InitialItem, item));

    private static RgfDashboardItem CreateItem(RgfDashboardViewType viewType, int settingsId)
        => new()
        {
            DashboardItemId = 1,
            Title = "Orders",
            ViewReference = new()
            {
                EntityName = "Orders",
                ViewType = viewType,
                SettingsId = settingsId
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

    private sealed class DashboardItemHostComponent : ComponentBase
    {
        [Parameter]
        public RgfDashboardItem InitialItem { get; set; } = null!;

        private RgfDashboardItem? _item;

        protected override void OnParametersSet()
        {
            _item ??= InitialItem;
        }

        public void SetItem(RgfDashboardItem item)
        {
            _item = item;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<RgfDashboardItemComponent>(0);
            builder.AddAttribute(1, nameof(RgfDashboardItemComponent.Item), _item);
            builder.AddAttribute(2, nameof(RgfDashboardItemComponent.LayoutCommitVersion), 0);
            builder.CloseComponent();
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

        public static void DispatchNextInitialized()
        {
            Assert.NotNull(PendingInitialization);
            var entityParameters = PendingInitialization;
            var manager = new FakeRgManager(entityParameters, new RgfEntity
            {
                EntityId = 1,
                EntityName = entityParameters.EntityName,
                Title = entityParameters.EntityName
            });
            PendingInitialization = null;
            entityParameters.EventDispatcher.DispatchEventAsync(
                RgfEntityEventKind.Initialized,
                new RgfEventArgs<RgfEntityEventArgs>(
                    sender: typeof(FakeEntityHostComponent),
                    args: new RgfEntityEventArgs(RgfEntityEventKind.Initialized, manager)))
                .GetAwaiter()
                .GetResult();
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

    private sealed class FakeRgManager(RgfSessionParams sessionParams, RgfEntity entityDesc) : IRgManager
    {
        public RgfSessionParams SessionParams { get; } = sessionParams;
        public IServiceProvider ServiceProvider => throw new NotSupportedException();
        public IRgfNotificationManager NotificationManager { get; } = new FakeNotificationManager();
        public IRgfNotificationManager ToastManager { get; } = new FakeNotificationManager();
        public IRgListHandler ListHandler => throw new NotSupportedException();
        public RgfEntity EntityDesc { get; } = entityDesc;
        public ObservableProperty<Dictionary<int, RgfEntityKey>> SelectedItems { get; } = new(new(), nameof(SelectedItems));
        public ObservableProperty<FormViewKey?> FormViewKey { get; } = new(new(), nameof(FormViewKey));
        public RgfSelectParam? SelectParam => null;
        public ObservableProperty<int> ItemCount { get; } = new(0, nameof(ItemCount));
        public ObservableProperty<int> PageSize { get; } = new(15, nameof(PageSize));
        public ObservableProperty<int> ActivePage { get; } = new(1, nameof(ActivePage));
        public List<RgfGridSetting> GridSettingList { get; } = [];
        public bool IsFiltered => false;
        public event EventHandler<CreateGridRequestEventArgs> CreateGridRequestCreated { add { } remove { } }
        public event Action<bool> RefreshEntity { add { } remove { } }
        public Task<IRgFilterHandler> GetFilterHandlerAsync() => throw new NotSupportedException();
        public Task InitFilterHandlerAsync(string condition) => throw new NotSupportedException();
        public bool IsColumnFiltered(IRgfProperty property, string? matchCriteria = null) => false;
        public Task<RgfResult<RgfFilterSetting>> SaveFilterSettingsAsync(RgfFilterSettings predefinedFilter) => throw new NotSupportedException();
        public Task<bool> DeleteFilterSettingsAsync(int filterSettingsId) => throw new NotSupportedException();
        public Task<RgfGridSetting?> SaveGridSettingsAsync(RgfGridSettings settings, bool recreate = false) => throw new NotSupportedException();
        public Task<bool> DeleteGridSettingsAsync(int gridSettingsId) => throw new NotSupportedException();
        public Task<List<RgfChartSettings>> GetChartSettingsListAsync() => Task.FromResult(new List<RgfChartSettings>());
        public Task<RgfChartSettings?> SaveChartSettingsAsync(RgfChartSettings settings, bool recreate = false) => throw new NotSupportedException();
        public Task<bool> DeleteChartSettingsAsync(int chartSettingsId) => throw new NotSupportedException();
        public RgfGridRequest CreateGridRequest(Action<RgfGridRequest>? create = null)
        {
            var request = RgfGridRequest.Create(SessionParams);
            create?.Invoke(request);
            return request;
        }
        public Task<RgfResult<RgfGridResult>> GetRecroGridAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<RgfResult<RgfEntity>> GetEntityDescAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<RgfResult<RgfGridResult>> GetAggregateDataAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<RgfResult<RgfCustomFunctionResult>> CallCustomFunctionAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<ResultType> GetResourceAsync<ResultType>(string name, Dictionary<string, string> query) where ResultType : class => throw new NotSupportedException();
        public Task<bool> RecreateAsync() => throw new NotSupportedException();
        public IRgFormHandler CreateFormHandler() => throw new NotSupportedException();
        public Task<RgfResult<RgfFormResult>> GetFormAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<RgfPropertyTooltips> GetPropertyTooltipsAsync() => throw new NotSupportedException();
        public Task<RgfResult<RgfFormResult>> UpdateFormDataAsync(RgfGridRequest request) => throw new NotSupportedException();
        public Task<RgfResult<RgfFormResult>> DeleteDataAsync(RgfEntityKey entityKey) => throw new NotSupportedException();
        public Task<int> DeleteSelectedItemsAsync() => throw new NotSupportedException();
        public Task BroadcastMessages(RgfCoreMessages messages, object sender, bool clearAfterBroadcast = true) => Task.CompletedTask;
        public Task OnToolbarCommandAsync(IRgfEventArgs<RgfToolbarEventArgs> arg) => throw new NotSupportedException();
        public Task<string> AboutAsync() => throw new NotSupportedException();
        public void Dispose() { }
    }

    private sealed class FakeNotificationManager : IRgfNotificationManager
    {
        public IRgfObservableEvent<TArgs> GetObservableEvents<TArgs>() where TArgs : EventArgs => throw new NotSupportedException();
        public Task RaiseEventAsync<TArgs>(TArgs args, object sender) where TArgs : EventArgs => Task.CompletedTask;
        public IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Action<IRgfEventArgs<TArgs>> handler) where TArgs : EventArgs => throw new NotSupportedException();
        public IRgfObserver<IRgfEventArgs<TArgs>> Subscribe<TArgs>(Func<IRgfEventArgs<TArgs>, Task> handler) where TArgs : EventArgs => throw new NotSupportedException();
        public void Dispose() { }
    }
}
