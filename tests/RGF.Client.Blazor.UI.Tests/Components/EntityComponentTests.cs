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

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class EntityComponentTests
{
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

    private static BunitContext CreateTestContext()
    {
        RgfClientBlazorUiTestState.Reset();

        var testContext = new BunitContext();
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService>(new FakeRecroSecService("eng"));
        testContext.Services.AddSingleton<IRgfEventNotificationService, FakeEventNotificationService>();
        testContext.Services.AddSingleton<IRgfApiService, PendingRgfApiService>();
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

    private sealed class PendingRgfApiService : IRgfApiService
    {
        public Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request) where ResultType : class
            => throw new NotSupportedException();

        public Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request) where ResultType : class
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
