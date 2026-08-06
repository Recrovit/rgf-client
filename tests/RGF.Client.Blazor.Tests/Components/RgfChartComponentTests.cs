using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Components;

[Collection("RGF.Client.Blazor.StaticState")]
public sealed class RgfChartComponentTests
{
    private const string CardRequiresSingleAggregateMessage = "Card charts require exactly one aggregate and do not support additional grouping.";

    [Fact]
    public void AllowedProperties_IncludeAutoExternal_AndNumericSelectorStillFiltersToNumericFields()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String),
                CreateProperty(2, "CustomerName", "Customer Name", PropertyFormType.TextBox, PropertyListType.String, autoExternalPath: "Customer/200"),
                CreateProperty(3, "CustomerRevenue", "Customer Revenue", PropertyFormType.TextBox, PropertyListType.Numeric, autoExternalPath: "Customer/201"),
                CreateProperty(4, "HiddenByAggregationExclude", "Hidden", PropertyFormType.TextBox, PropertyListType.Numeric, options: new Dictionary<string, object> { ["RGO_AggregationExclude"] = true })
            ]
        });

        var cut = RenderChartComponent(testContext, entityParameters);

        Assert.Contains(cut.Instance.AllowedProperties, property => property.Id == 2);
        Assert.Contains(cut.Instance.AllowedProperties, property => property.Id == 3);
        Assert.DoesNotContain(cut.Instance.AllowedProperties, property => property.Id == 4);
        Assert.Contains(cut.Instance.ChartColumnsNumeric, option => option.Key == 3 && option.Value == "Customer Revenue");
        Assert.DoesNotContain(cut.Instance.ChartColumnsNumeric, option => option.Key == 2);
        Assert.DoesNotContain(cut.Instance.ChartColumnsNumeric, option => option.Key == 4);
    }

    [Fact]
    public async Task OnShowChart_RefreshesAllowedProperties_OnEveryOpen()
    {
        using var testContext = CreateTestContext();
        var manager = new FakeRgManager(new RgfSessionParams(), new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String),
                CreateProperty(2, "Amount", "Amount", PropertyFormType.TextBox, PropertyListType.Numeric)
            ]
        });
        var entityParameters = CreateEntityParameters(manager.EntityDesc, (_, _) => manager);

        var cut = RenderChartComponent(testContext, entityParameters);

        Assert.DoesNotContain(cut.Instance.AllowedProperties, property => property.Id == 3);

        manager.EntityDesc = new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String),
                CreateProperty(2, "Amount", "Amount", PropertyFormType.TextBox, PropertyListType.Numeric),
                CreateProperty(3, "CustomerRevenue", "Customer Revenue", PropertyFormType.TextBox, PropertyListType.Numeric, autoExternalPath: "Customer/201")
            ]
        };

        await cut.InvokeAsync(() => entityParameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(
            RgfToolbarEventKind.RecroChart,
            new RgfEventArgs<RgfToolbarEventArgs>(cut.Instance, new RgfToolbarEventArgs(RgfToolbarEventKind.RecroChart))));

        Assert.Contains(cut.Instance.AllowedProperties, property => property.Id == 3);
    }

    [Fact]
    public void Validation_MarksMissingPropertyIdsAsInvalid()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String),
                CreateProperty(2, "Amount", "Amount", PropertyFormType.TextBox, PropertyListType.Numeric)
            ]
        });

        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettings.AggregationSettings.Columns.Clear();
        cut.Instance.ChartSettings.AggregationSettings.Columns.Add(new RgfAggregationColumn { Id = 999, Aggregate = "Sum" });
        cut.Instance.ChartSettings.AggregationSettings.Groups.Add(new RgfIdAliasPair(888, "MissingGroup"));
        cut.Instance.ChartSettings.AggregationSettings.SubGroup.Add(new RgfIdAliasPair(777, "MissingSubGroup"));

        var isValid = cut.Instance.EditContext.Validate();

        Assert.False(isValid);
        Assert.NotEmpty(cut.Instance.EditContext.GetValidationMessages(() => cut.Instance.ChartSettings.AggregationSettings.Columns[0]));
        Assert.NotEmpty(cut.Instance.EditContext.GetValidationMessages(() => cut.Instance.ChartSettings.AggregationSettings.Groups[0]));
        Assert.NotEmpty(cut.Instance.EditContext.GetValidationMessages(() => cut.Instance.ChartSettings.AggregationSettings.SubGroup[0]));
    }

    [Fact]
    public async Task OnShowChart_RevalidatesExistingChartSettings_AfterMetadataRefresh()
    {
        using var testContext = CreateTestContext();
        var manager = new FakeRgManager(new RgfSessionParams(), new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String),
                CreateProperty(2, "Amount", "Amount", PropertyFormType.TextBox, PropertyListType.Numeric)
            ]
        });
        var entityParameters = CreateEntityParameters(manager.EntityDesc, (_, _) => manager);
        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettings.AggregationSettings.Columns.Clear();
        cut.Instance.ChartSettings.AggregationSettings.Columns.Add(new RgfAggregationColumn { Id = 2, Aggregate = "Sum" });

        manager.EntityDesc = new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Name", "Name", PropertyFormType.TextBox, PropertyListType.String)
            ]
        };

        await cut.InvokeAsync(() => entityParameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(
            RgfToolbarEventKind.RecroChart,
            new RgfEventArgs<RgfToolbarEventArgs>(cut.Instance, new RgfToolbarEventArgs(RgfToolbarEventKind.RecroChart))));

        Assert.DoesNotContain(cut.Instance.AllowedProperties, property => property.Id == 2);
        Assert.NotEmpty(cut.Instance.EditContext.GetValidationMessages(() => cut.Instance.ChartSettings.AggregationSettings.Columns[0]));
    }

    [Fact]
    public void CreateCardModel_ReturnsFormattedCard_ForSingleAggregateResult()
    {
        using var testContext = CreateTestContext(userLanguage: "hun");
        var entityParameters = CreateEntityParameters(new RgfEntity
        {
            EntityId = 1,
            EntityName = "Orders",
            EntityVersion = "1",
            MenuTitle = "Orders",
            Title = "Orders",
            Permissions = new RgfPermissions(true),
            Properties =
            [
                CreateProperty(1, "Amount", "Nettó árbevétel", PropertyFormType.TextBox, PropertyListType.Numeric)
            ]
        });

        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("hu-HU");
        CultureInfo.CurrentUICulture = new CultureInfo("hu-HU");
        try
        {
            var cut = RenderChartComponent(testContext, entityParameters);
            cut.Instance.ChartSettings.SeriesType = RgfChartSeriesType.Card;
            cut.Instance.ChartSettings.Remark = " kedvezmények után ";
            cut.Instance.DataColumns =
            [
                CreateColumn("Amount_Sum", "Nettó árbevétel", "Sum")
            ];
            cut.Instance.ChartData =
            [
                CreateChartData("Amount_Sum", 1265793m)
            ];
            SetProcessingStatus(cut.Instance, nameof(RgfChartComponent.DataStatus), RgfProcessingStatus.Valid);

            var cardModel = cut.Instance.CreateCardModel();

            Assert.NotNull(cardModel);
            Assert.Equal("Nettó árbevétel", cardModel!.Title);
            Assert.Equal("1 265 793", cardModel.Value);
            Assert.Equal("kedvezmények után", cardModel.Remark);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void CreateCardModel_ReturnsNull_WhenMultipleRowsAreAvailable()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
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
        });

        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettings.SeriesType = RgfChartSeriesType.Card;
        cut.Instance.DataColumns =
        [
            CreateColumn("Amount_Sum", "Amount", "Sum")
        ];
        cut.Instance.ChartData =
        [
            CreateChartData("Amount_Sum", 1m),
            CreateChartData("Amount_Sum", 2m)
        ];
        SetProcessingStatus(cut.Instance, nameof(RgfChartComponent.DataStatus), RgfProcessingStatus.Valid);

        var cardModel = cut.Instance.CreateCardModel();

        Assert.Null(cardModel);
    }

    [Fact]
    public async Task SaveChartSettingsAsync_PreservesRemarkInSavedSettingList()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
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
        }, managerFactory: (sessionParams, entity) => new FakeRgManager(sessionParams, entity)
        {
            SavedChartSettingsResult = new RgfChartSettings
            {
                ChartSettingsId = 42,
                RoleId = "Managers"
            }
        });

        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettings.SettingsName = "Revenue card";
        cut.Instance.ChartSettings.Remark = "kedvezmények után";

        var success = await cut.Instance.SaveChartSettingsAsync();

        Assert.True(success);
        Assert.Single(cut.Instance.ChartSettingList);
        Assert.Equal("kedvezmények után", cut.Instance.ChartSettingList[0].Remark);
    }

    [Fact]
    public async Task OnSetChartSettingAsync_PreservesRemarkWhenLoadingSavedSetting()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
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
        });

        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettingList.Add(new RgfChartSettings
        {
            ChartSettingsId = 7,
            SettingsName = "Revenue card",
            Remark = "kedvezmények után"
        });

        var success = await cut.Instance.OnSetChartSettingAsync(7, "Revenue card");

        Assert.True(success);
        Assert.Equal("kedvezmények után", cut.Instance.ChartSettings.Remark);
    }

    [Fact]
    public void DeepCopy_PreservesAggregationSortAndLimitSettings()
    {
        var source = new RgfChartSettings
        {
            AggregationSettings = new RgfAggregationSettings
            {
                Columns =
                [
                    new RgfAggregationColumn { Id = 1, Aggregate = "Sum", Sort = -2 },
                    new RgfAggregationColumn { Id = 0, Aggregate = "Count", Sort = 1 }
                ],
                Take = 5
            }
        };

        var copy = RgfChartSettings.DeepCopy(source);

        Assert.NotSame(source, copy);
        Assert.NotNull(copy);
        Assert.Equal(2, copy!.AggregationSettings.Columns.Count);
        Assert.Equal(-2, copy.AggregationSettings.Columns[0].Sort);
        Assert.Equal(1, copy.AggregationSettings.Columns[1].Sort);
        Assert.Equal(5, copy.AggregationSettings.Take);
    }

    [Fact]
    public async Task AggregationSortAndTake_DoNotReorderExistingClientChartData()
    {
        using var testContext = CreateTestContext();
        var entityParameters = CreateEntityParameters(new RgfEntity
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
        });

        var cut = RenderChartComponent(testContext, entityParameters);
        cut.Instance.ChartSettings.AggregationSettings.Columns.Clear();
        var aggregateColumn = new RgfAggregationColumn { Id = 1, Aggregate = "Sum" };
        cut.Instance.ChartSettings.AggregationSettings.Columns.Add(aggregateColumn);
        cut.Instance.ChartSettings.AggregationSettings.Take = 2;
        cut.Instance.ChartData =
        [
            CreateChartData(("Category", "B")),
            CreateChartData(("Category", "A")),
            CreateChartData(("Category", "C"))
        ];

        await cut.InvokeAsync(() =>
        {
            cut.Instance.SetColumnSortPriority(aggregateColumn, 1);
            cut.Instance.SetColumnSortDescending(aggregateColumn, true);
        });

        Assert.Equal(-1, aggregateColumn.Sort);
        Assert.Equal(2, cut.Instance.ChartSettings.AggregationSettings.Take);
        Assert.Equal(["B", "A", "C"], cut.Instance.ChartData.Select(row => row.Get<string>("Category")).ToArray());
    }

    private static BunitContext CreateTestContext(string userLanguage = "eng")
    {
        RgfBlazorTestState.Reset();
        RgfBlazorConfiguration.RegisterComponent<FakeDialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
        RgfBlazorConfiguration.RegisterEntityComponent<FakeEntityHostComponent>(string.Empty);

        var testContext = new BunitContext();
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService>(new FakeRecroSecService(userLanguage));
        return testContext;
    }

    private static IRenderedComponent<RgfChartComponent> RenderChartComponent(BunitContext testContext, RgfEntityParameters entityParameters)
        => testContext.Render<RgfChartComponent>(parameters => parameters
            .Add(component => component.EntityParameters, entityParameters)
            .Add(component => component.ContentTemplate, (RenderFragment<RgfChartComponent>)(_ => builder => { }))
            .Add(component => component.FooterTemplate, (RenderFragment<RgfChartComponent>)(_ => builder => { })));

    private static RgfEntityParameters CreateEntityParameters(RgfEntity entity, Func<RgfSessionParams, RgfEntity, IRgManager>? managerFactory = null)
    {
        var entityParameters = new RgfEntityParameters(entity.EntityName, new RgfSessionParams());
        entityParameters.DialogTemplate = _ => builder => { };
        typeof(RgfEntityParameters).GetProperty(nameof(RgfEntityParameters.Manager), BindingFlags.Instance | BindingFlags.Public)
            ?.SetValue(entityParameters, managerFactory?.Invoke(entityParameters, entity) ?? new FakeRgManager(entityParameters, entity));
        return entityParameters;
    }

    private static RgfProperty CreateProperty(
        int id,
        string alias,
        string title,
        PropertyFormType formType,
        PropertyListType listType,
        string? autoExternalPath = null,
        Dictionary<string, object>? options = null)
    {
        options ??= new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(autoExternalPath))
        {
            options["RGO_AutoExternal"] = autoExternalPath;
        }

        return new RgfProperty
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
            Options = options,
            Ex = string.Empty
        };
    }

    private static RgfDynamicDictionary CreateColumn(string alias, string name, string aggregate = null!, IRgfProperty? property = null)
    {
        var column = new RgfDynamicDictionary();
        column.SetMember("Alias", alias);
        column.SetMember("Name", name);
        column.SetMember("Aggregate", aggregate);
        if (property != null)
        {
            column.SetMember("Property", property);
        }
        return column;
    }

    private static RgfDynamicDictionary CreateChartData(string alias, decimal value)
    {
        var row = new RgfDynamicDictionary();
        row.SetMember(alias, value);
        return row;
    }

    private static RgfDynamicDictionary CreateChartData(params (string Alias, object Value)[] members)
    {
        var row = new RgfDynamicDictionary();
        foreach (var member in members)
        {
            row.SetMember(member.Alias, member.Value);
        }
        return row;
    }

    private static void SetProcessingStatus(RgfChartComponent component, string propertyName, RgfProcessingStatus value)
        => typeof(RgfChartComponent)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(component, value);

    private sealed class FakeRgManager(RgfSessionParams sessionParams, RgfEntity entityDesc) : IRgManager
    {
        public RgfChartSettings? SavedChartSettingsResult { get; set; }

        public RgfSessionParams SessionParams { get; } = sessionParams;

        public IServiceProvider ServiceProvider => throw new NotSupportedException();

        public IRgfNotificationManager NotificationManager { get; } = new FakeNotificationManager();

        public IRgfNotificationManager ToastManager { get; } = new FakeNotificationManager();

        public IRgListHandler ListHandler => throw new NotSupportedException();

        public RgfEntity EntityDesc { get; set; } = entityDesc;

        public ObservableProperty<Dictionary<int, RgfEntityKey>> SelectedItems { get; } = new(new(), nameof(SelectedItems));

        public ObservableProperty<FormViewKey?> FormViewKey { get; } = new(new(), nameof(FormViewKey));

        public RgfSelectParam? SelectParam => null;

        public ObservableProperty<int> ItemCount { get; } = new(0, nameof(ItemCount));

        public ObservableProperty<int> PageSize { get; } = new(15, nameof(PageSize));

        public ObservableProperty<int> ActivePage { get; } = new(1, nameof(ActivePage));

        public List<RgfGridSetting> GridSettingList { get; } = [];

        public bool IsFiltered => false;

        public event EventHandler<CreateGridRequestEventArgs> CreateGridRequestCreated
        {
            add { }
            remove { }
        }

        public event Action<bool> RefreshEntity
        {
            add { }
            remove { }
        }

        public Task<IRgFilterHandler> GetFilterHandlerAsync() => Task.FromResult<IRgFilterHandler>(new FakeFilterHandler());
        public Task InitFilterHandlerAsync(string condition) => throw new NotSupportedException();
        public bool IsColumnFiltered(IRgfProperty property, string? matchCriteria = null) => false;
        public Task<RgfResult<RgfFilterSetting>> SaveFilterSettingsAsync(RgfFilterSettings predefinedFilter) => throw new NotSupportedException();
        public Task<bool> DeleteFilterSettingsAsync(int filterSettingsId) => throw new NotSupportedException();
        public Task<RgfGridSetting?> SaveGridSettingsAsync(RgfGridSettings settings, bool recreate = false) => throw new NotSupportedException();
        public Task<bool> DeleteGridSettingsAsync(int gridSettingsId) => throw new NotSupportedException();
        public Task<List<RgfChartSettings>> GetChartSettingsListAsync() => Task.FromResult(new List<RgfChartSettings>());
        public Task<RgfChartSettings?> SaveChartSettingsAsync(RgfChartSettings settings, bool recreate = false)
            => Task.FromResult(SavedChartSettingsResult);
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

    private sealed class FakeFilterHandler : IRgFilterHandler
    {
        public List<RgfFilter.Condition> Conditions { get; } = [];
        public List<RgfFilterSettings> PredefinedFilters { get; } = [];
        public RgfFilterProperty[] RgfFilterProperties { get; } = [];
        public bool IsColumnFiltered(IRgfProperty property, string? matchCriteria = null) => false;
        public Task SetQuickFilterAsync(IRgfProperty property, object? condition) => Task.CompletedTask;
        public int FindCondition(IList<RgfFilter.Condition> conditions, int clientId, out RgfFilter.Condition condition)
        {
            condition = new RgfFilter.Condition();
            return -1;
        }
        public void AddBracket(int clientId) { }
        public RgfFilter.Condition? AddCondition(Microsoft.Extensions.Logging.ILogger logger, int clientId) => null;
        public bool ChangeProperty(RgfFilter.Condition condition, int newPropertyId) => false;
        public bool ChangeQueryOperator(Microsoft.Extensions.Logging.ILogger logger, RgfFilter.Condition condition, RgfFilter.QueryOperator newOperator) => false;
        public bool InitFilter(string? jsonCondition) => true;
        public void RemoveBracket(int clientId) { }
        public void RemoveCondition(int clientId) { }
        public bool ResetFilter() => true;
        public void ApplyFilterState(IEnumerable<RgfFilter.Condition>? conditions, int? sqlTimeout) { }
        public Task SetFilterAsync(IEnumerable<RgfFilter.Condition>? conditions, int? sqlTimeout) => Task.CompletedTask;
        public RgfFilterSettings? SelectPredefinedFilter(int? filterSettingsId) => null;
        public Task<bool> SaveFilterSettingsAsync(RgfFilterSettings filterSettings) => Task.FromResult(true);
        public Task<bool> DeleteFilterSettingsAsync(int filterSettingsId) => Task.FromResult(true);
        public RgfFilter.Condition[] StoreFilter() => [];
    }

    private sealed class FakeRecroDictService : IRecroDictService
    {
        public bool IsInitialized => true;
        public Dictionary<string, string> Languages { get; } = [];
        public string DefaultLanguage => "eng";
        public Task InitializeAsync(string language = null!) => Task.CompletedTask;
        public Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
            => Task.FromResult(new ConcurrentDictionary<string, string>());
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

    private sealed class FakeDialogComponent : ComponentBase
    {
        [Parameter]
        public RgfDialogParameters DialogParameters { get; set; } = null!;
    }

    private sealed class FakeEntityHostComponent : ComponentBase
    {
        [Parameter]
        public RgfEntityParameters EntityParameters { get; set; } = null!;
    }
}
