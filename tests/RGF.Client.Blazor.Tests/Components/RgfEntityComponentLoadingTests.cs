using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Components;

[Collection("RGF.Client.Blazor.StaticState")]
public sealed class RgfEntityComponentLoadingTests
{
    [Fact]
    public void LoadingIndicator_UsesExplicitOverrideBeforeRegisteredComponent()
    {
        using var testContext = CreateTestContext();
        RgfBlazorConfiguration.RegisterComponent<FakeLoadingComponent>(RgfBlazorConfiguration.ComponentType.LoadingIndicator);

        var cut = RenderComponent(testContext, CreateEntityParameters(), parameters => parameters
            .Add(component => component.LoadingIndicator, (RenderFragment)(builder => builder.AddContent(0, "Custom loading"))));

        Assert.Contains("Custom loading", cut.Markup);
        Assert.DoesNotContain("loading-from-component", cut.Markup);
    }

    [Fact]
    public void LoadingIndicator_UsesRegisteredComponentAndPassesProvidedParameters()
    {
        using var testContext = CreateTestContext();
        RgfBlazorConfiguration.RegisterComponent<FakeLoadingComponent>(RgfBlazorConfiguration.ComponentType.LoadingIndicator);
        var loadingIndicatorParameters = new RgfLoadingIndicatorParameters
        {
            Text = "Please wait",
            Status = "Loading records",
            Style = "width: 5rem; height: 5rem;"
        };

        var cut = RenderComponent(testContext, CreateEntityParameters(), parameters => parameters
            .Add(component => component.LoadingIndicatorParameters, loadingIndicatorParameters));

        Assert.Contains("loading-from-component", cut.Markup);
        Assert.Contains("Please wait", cut.Markup);
        Assert.Contains("Loading records", cut.Markup);
        Assert.Contains("width: 5rem; height: 5rem;", cut.Markup);
    }

    [Fact]
    public void LoadingIndicator_FallsBackToBuiltInTextWhenNothingIsRegistered()
    {
        using var testContext = CreateTestContext();

        var cut = RenderComponent(testContext, CreateEntityParameters());

        Assert.Contains("Loading...", cut.Markup);
        Assert.DoesNotContain("loading-from-component", cut.Markup);
    }

    private static BunitContext CreateTestContext()
    {
        RgfBlazorTestState.Reset();

        var testContext = new BunitContext();
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService>(new FakeRecroSecService("eng"));
        testContext.Services.AddSingleton<IRgfEventNotificationService, FakeEventNotificationService>();
        testContext.Services.AddSingleton<IRgfApiService, PendingRgfApiService>();
        return testContext;
    }

    private static RgfEntityParameters CreateEntityParameters()
    {
        var sessionParams = new RgfSessionParams
        {
            GridId = "1",
            SessionId = Guid.NewGuid().ToString("N")
        };
        return new RgfEntityParameters("Orders", sessionParams);
    }

    private static IRenderedComponent<RgfEntityComponent> RenderComponent(
        BunitContext testContext,
        RgfEntityParameters entityParameters,
        Action<ComponentParameterCollectionBuilder<RgfEntityComponent>>? configure = null)
        => testContext.Render<RgfEntityComponent>(parameters =>
        {
            parameters.Add(component => component.EntityParameters, entityParameters);
            parameters.Add(component => component.ToolbarTemplate, CreateTemplate<RgfEntityParameters>());
            parameters.Add(component => component.GridTemplate, CreateTemplate<RgfEntityParameters>());
            parameters.Add(component => component.FilterTemplate, CreateTemplate<RgfEntityParameters>());
            parameters.Add(component => component.PagerTemplate, CreateTemplate<RgfEntityParameters>());
            parameters.Add(component => component.FormTemplate, CreateTemplate<RgfEntityParameters>());
            configure?.Invoke(parameters);
        });

    private static RenderFragment<TValue> CreateTemplate<TValue>() => _ => builder => { };

    private sealed class FakeLoadingComponent : ComponentBase
    {
        [Parameter]
        public RgfLoadingIndicatorParameters LoadingIndicatorParameters { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, "loading-from-component");
            builder.OpenElement(2, "span");
            builder.AddContent(3, LoadingIndicatorParameters.Text ?? "text-null");
            builder.CloseElement();
            builder.OpenElement(4, "span");
            builder.AddContent(5, LoadingIndicatorParameters.Status ?? "status-null");
            builder.CloseElement();
            builder.OpenElement(6, "span");
            builder.AddContent(7, LoadingIndicatorParameters.Style ?? "style-null");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private sealed class PendingRgfApiService : IRgfApiService
    {
        public Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request)
            where ResultType : class
            => throw new NotSupportedException();

        public Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request)
            where ResultType : class
        {
            if (typeof(ResultType) == typeof(RgfResult<RgfGridResult>) &&
                string.Equals(request.Uri, "/rgf/api/entity/RecroGrid", StringComparison.Ordinal))
            {
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
}
