using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Client;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Tests.Services;

public sealed class RecroSecServiceTests
{
    [Fact]
    public void RecroSecService_InitializesWithDefaultNonNullUserState()
    {
        using var serviceProvider = CreateServiceProvider(new TestAuthenticationStateProvider(), new FakeRecroSecApiService());
        using var scope = serviceProvider.CreateScope();
        var recroSec = scope.ServiceProvider.GetRequiredService<IRecroSecService>();

        Assert.NotNull(recroSec.UserState);
        Assert.False(recroSec.UserState.IsValid);
        Assert.False(recroSec.UserState.IsAdmin);
        Assert.Empty(recroSec.Roles);
    }

    [Fact]
    public void RgfUserState_DeepCopy_CreatesIndependentReadOnlySnapshot()
    {
        var roles = new Dictionary<string, string>(StringComparer.Ordinal) { ["admin"] = "Admin" };
        var settings = new Dictionary<string, string>(StringComparer.Ordinal) { [RgfUserStateSettingKeys.Theme] = "Dark" };
        var source = new RgfUserState
        {
            IsValid = true,
            IsAdmin = true,
            UserName = "alice",
            Language = "eng",
            Roles = roles,
            Settings = settings
        };

        var copy = RgfUserState.DeepCopy(source);

        Assert.NotNull(copy);
        Assert.NotSame(source, copy);
        Assert.NotSame(source.Roles, copy.Roles);
        Assert.NotSame(source.Settings, copy.Settings);
        Assert.Equal("Dark", copy.Settings![RgfUserStateSettingKeys.Theme]);

        roles["admin"] = "Changed";
        settings[RgfUserStateSettingKeys.Theme] = "Light";

        Assert.Equal("Admin", copy.Roles!["admin"]);
        Assert.Equal("Dark", copy.Settings![RgfUserStateSettingKeys.Theme]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)copy.Roles!).Add("user", "User"));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)copy.Settings!).Add(RgfUserStateSettingKeys.Size, "Large"));
    }

    [Fact]
    public void RgfUserState_SerializesAndDeserializes_WithReadOnlyDictionaryProperties()
    {
        var source = new RgfUserState
        {
            IsValid = true,
            Language = "hun",
            Settings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RgfUserStateSettingKeys.Language] = "hun"
            }
        };

        var json = JsonSerializer.Serialize(source, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var deserialized = JsonSerializer.Deserialize<RgfUserState>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsValid);
        Assert.Equal("hun", deserialized.Language);
        Assert.Equal("hun", deserialized.Settings![RgfUserStateSettingKeys.Language]);
    }

    [Fact]
    public async Task UserStateChangedEvent_PublishesImmutableSnapshot_And_UserStateMatchesPublishedValue()
    {
        var authProvider = new TestAuthenticationStateProvider();
        var apiService = new FakeRecroSecApiService
        {
            UserStateResponse = new RgfUserState
            {
                IsValid = true,
                UserName = "alice",
                Language = "eng",
                Roles = new Dictionary<string, string>(StringComparer.Ordinal) { ["admin"] = "Admin" },
                Settings = new Dictionary<string, string>(StringComparer.Ordinal) { [RgfUserStateSettingKeys.Theme] = "Dark" }
            }
        };

        using var serviceProvider = CreateServiceProvider(authProvider, apiService);
        using var scope = serviceProvider.CreateScope();
        var recroSec = scope.ServiceProvider.GetRequiredService<IRecroSecService>();
        var eventSourceState = apiService.UserStateResponse;
        var publishedStateTask = new TaskCompletionSource<RgfUserState>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.False(recroSec.UserState.IsValid);

        recroSec.UserStateChangedEvent.Subscribe(args =>
        {
            if (args.Value.IsValid)
            {
                publishedStateTask.TrySetResult(args.Value);
            }
            return Task.CompletedTask;
        });

        authProvider.SetAuthenticatedUser("alice");

        var publishedState = await publishedStateTask.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Same(recroSec.UserState, publishedState);
        Assert.NotSame(eventSourceState, publishedState);
        Assert.Equal("Dark", publishedState.Settings![RgfUserStateSettingKeys.Theme]);
        Assert.Equal("Admin", recroSec.Roles["admin"]);

        ((Dictionary<string, string>)eventSourceState.Settings!)[RgfUserStateSettingKeys.Theme] = "Light";
        ((Dictionary<string, string>)eventSourceState.Roles!)["admin"] = "Changed";

        Assert.Equal("Dark", recroSec.UserState.Settings![RgfUserStateSettingKeys.Theme]);
        Assert.Equal("Admin", recroSec.Roles["admin"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)recroSec.Roles).Add("user", "User"));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)publishedState.Settings!).Add(RgfUserStateSettingKeys.Size, "Large"));
    }

    [Fact]
    public async Task UpdateUserStateSettingsAsync_UsesCopyOnWrite_And_UpdatesPublishedSnapshot()
    {
        var authProvider = new TestAuthenticationStateProvider();
        var apiService = new FakeRecroSecApiService
        {
            UserStateResponse = new RgfUserState
            {
                IsValid = true,
                UserName = "alice",
                Language = "eng",
                Settings = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [RgfUserStateSettingKeys.Theme] = "Dark",
                    [RgfUserStateSettingKeys.Language] = "eng"
                }
            }
        };

        using var serviceProvider = CreateServiceProvider(authProvider, apiService);
        using var scope = serviceProvider.CreateScope();
        var recroSec = scope.ServiceProvider.GetRequiredService<IRecroSecService>();

        authProvider.SetAuthenticatedUser("alice");
        await WaitForAsync(() => recroSec.UserState.Settings?.ContainsKey(RgfUserStateSettingKeys.Theme) == true);

        var previousState = recroSec.UserState;
        var updated = await recroSec.UpdateUserStateSettingsAsync(new Dictionary<string, string?>
        {
            [RgfUserStateSettingKeys.Theme] = "Light",
            [RgfUserStateSettingKeys.Language] = null
        });

        Assert.True(updated);
        Assert.NotSame(previousState, recroSec.UserState);
        Assert.Equal("Light", recroSec.UserState.Settings![RgfUserStateSettingKeys.Theme]);
        Assert.False(recroSec.UserState.Settings.ContainsKey(RgfUserStateSettingKeys.Language));
        Assert.Null(recroSec.UserState.Language);
        Assert.Single(apiService.SavedSettings);
        Assert.Equal("Light", apiService.SavedSettings[0][RgfUserStateSettingKeys.Theme]);
        Assert.Null(apiService.SavedSettings[0][RgfUserStateSettingKeys.Language]);
    }

    private static ServiceProvider CreateServiceProvider(TestAuthenticationStateProvider authProvider, FakeRecroSecApiService apiService)
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRgfServices(configuration, NullLogger.Instance);
        services.AddSingleton<AuthenticationStateProvider>(authProvider);
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<IRgfApiService>(apiService);
        services.AddSingleton<IRecroDictService, FakeRecroDictService>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://example.test"
            })
            .Build();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("Condition was not met within the allotted time.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class FakeRecroSecApiService : IRgfApiService
    {
        public RgfUserState UserStateResponse { get; set; } = new();

        public List<Dictionary<string, string?>> SavedSettings { get; } = [];

        public Task<IRgfApiResponse<ResultType>> GetAsync<ResultType>(IRgfApiRequest request) where ResultType : class
        {
            if (request.Uri.EndsWith("/rgf/api/recrosec/UserState", StringComparison.Ordinal)
                && typeof(ResultType) == typeof(RgfUserState))
            {
                return Task.FromResult<IRgfApiResponse<ResultType>>(new ApiResponse<ResultType>
                {
                    Success = true,
                    Result = (ResultType)(object)UserStateResponse
                });
            }

            throw new NotSupportedException($"Unsupported GET request: {request.Uri}");
        }

        public async Task<IRgfApiResponse<ResultType>> PostAsync<ResultType>(IRgfApiRequest request) where ResultType : class
        {
            if (request.Uri.EndsWith("/rgf/api/recrosec/UserStateSettings", StringComparison.Ordinal)
                && typeof(ResultType) == typeof(RgfEmptyResult))
            {
                var content = await request.Content.ReadAsStringAsync(request.CancellationToken);
                SavedSettings.Add(JsonSerializer.Deserialize<Dictionary<string, string?>>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []);

                return new ApiResponse<ResultType>
                {
                    Success = true,
                    Result = (ResultType)(object)new RgfEmptyResult()
                };
            }

            throw new NotSupportedException($"Unsupported POST request: {request.Uri}");
        }
    }

    private sealed class FakeRecroDictService : IRecroDictService
    {
        public bool IsInitialized { get; private set; }

        public Dictionary<string, string> Languages { get; } = new(StringComparer.Ordinal)
        {
            ["eng"] = "English",
            ["hun"] = "Hungarian"
        };

        public string DefaultLanguage => "eng";

        public Task InitializeAsync(string language = null!)
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
            => Task.FromResult(new ConcurrentDictionary<string, string>());

        public string GetRgfUiString(string resourceKey) => resourceKey;

        public string GetRgfUiString(string resourceKey, params object[] args) => resourceKey;
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _authenticationState = new(new ClaimsPrincipal(new ClaimsIdentity()));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_authenticationState);

        public void SetAuthenticatedUser(string userName)
        {
            _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, userName)],
                authenticationType: "TestAuth")));

            NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://example.test/", "https://example.test/current");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
