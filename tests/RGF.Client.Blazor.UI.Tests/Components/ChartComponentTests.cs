using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Reflection;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class ChartComponentTests
{
    [Fact]
    public void ApexChartComponent_RendersCardRemark_WhenCardModeIsActive()
    {
        using var testContext = CreateTestContext();

        var cut = testContext.Render<ApexChartComponent>(parameters => parameters
            .Add(component => component.ChartSettings, new ApexChartSettings
            {
                ChartType = RgfChartSeriesType.Card,
                ShowDataLabels = true,
                Width = 420,
                Height = 180,
                Card = new RgfChartCardModel
                {
                    Title = "Nettó árbevétel",
                    Value = "1 265 793",
                    Remark = "kedvezmények után",
                }
            }));

        Assert.Contains("rgf-apexchart-card-view", cut.Markup);
        Assert.Contains("width:420px", cut.Markup);
        Assert.Contains("height:180px", cut.Markup);
        Assert.Contains("Nettó árbevétel", cut.Markup);
        Assert.Contains("1 265 793", cut.Markup);
        Assert.Contains("kedvezmények után", cut.Markup);
    }

    [Fact]
    public void ChartComponent_TracksCardType_WhenCardTypeIsSelected()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters();

        var cut = testContext.Render<ChartComponent>(parameters => parameters
            .Add(component => component.EntityParameters, entityParameters));

        var changeChartType = typeof(BaseChartComponent).GetMethod("ChangeChartType", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Task)changeChartType.Invoke(cut.Instance, [RgfChartSeriesType.Card])!).GetAwaiter().GetResult();
        cut.Render();

        var apexChartSettings = (ApexChartSettings)typeof(BaseChartComponent)
            .GetProperty("ApexChartSettings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;

        Assert.Equal(RgfChartSeriesType.Card, apexChartSettings.ChartType);
    }

    private static BunitContext CreateTestContext()
    {
        RgfClientBlazorUiTestState.Reset();
        RgfBlazorConfiguration.RegisterComponent<FakeDialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
        RgfBlazorConfiguration.RegisterEntityComponent<FakeEntityHostComponent>(string.Empty);

        var testContext = new BunitContext();
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        return testContext;
    }

    private static RgfEntityParameters CreateEntityParameters()
    {
        var entity = new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Amount", "Amount", PropertyFormType.TextBox, PropertyListType.Numeric)
            ]
        };

        var entityParameters = new RgfEntityParameters(entity.EntityName, new RgfSessionParams());
        entityParameters.DialogTemplate = _ => builder => { };
        typeof(RgfEntityParameters).GetProperty(nameof(RgfEntityParameters.Manager), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entityParameters, new FakeRgManager(entityParameters, entity));
        return entityParameters;
    }

    private static RgfProperty CreateProperty(int id, string alias, string title, PropertyFormType formType, PropertyListType listType)
        => new()
        {
            Id = id,
            Alias = alias,
            ClientName = alias,
            ColTitle = title,
            Readable = true,
            Editable = true,
            Orderable = true,
            FormType = formType,
            ListType = listType,
            Options = new Dictionary<string, object>(),
            Ex = string.Empty
        };

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

    private sealed class FakeDialogComponent : ComponentBase
    {
    }

    private sealed class FakeEntityHostComponent : ComponentBase
    {
    }
}
