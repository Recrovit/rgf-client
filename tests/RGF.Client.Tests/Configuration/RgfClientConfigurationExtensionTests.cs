using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;
using Recrovit.RecroGridFramework.Client.Tests.Testing;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Tests.Configuration;

public sealed class RgfClientConfigurationExtensionTests : IDisposable
{
    public RgfClientConfigurationExtensionTests()
    {
        RgfClientTestState.Reset();
    }

    public void Dispose()
    {
        RgfClientTestState.Reset();
    }

    [Fact]
    public void AddRgfServices_RegistersRequiredServices_AndConfiguresHttpClients()
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(CreateConfiguration());

        AssertContainsDescriptor<IRgfApiService, ApiService>(services, ServiceLifetime.Singleton);
        AssertContainsDescriptor<IRgfAccessTokenAccessor, NoOpRgfAccessTokenAccessor>(services, ServiceLifetime.Scoped);
        AssertContainsDescriptor<IRgfAuthenticationFailureHandler, NoOpRgfAuthenticationFailureHandler>(services, ServiceLifetime.Singleton);
        AssertContainsDescriptor<IRgfEventNotificationService>(services, ServiceLifetime.Scoped);
        AssertContainsDescriptor<IRecroSecService>(services, ServiceLifetime.Scoped);
        AssertContainsDescriptor<IRecroDictService>(services, ServiceLifetime.Scoped);
        AssertContainsDescriptor<IRgfMenuService>(services, ServiceLifetime.Scoped);
        AssertContainsDescriptor<IRgfProgressService, RgfProgressService>(services, ServiceLifetime.Transient);

        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var apiClient = httpClientFactory.CreateClient(ApiService.RgfApiClientName);
        var authClient = httpClientFactory.CreateClient(ApiService.RgfAuthApiClientName);
        Assert.NotNull(serviceProvider.GetRequiredService<IRgfEventNotificationService>());

        Assert.Equal(new Uri("https://api.example.test"), apiClient.BaseAddress);
        Assert.Equal(new Uri("https://api.example.test"), authClient.BaseAddress);
        Assert.Equal("https://api.example.test", ApiService.BaseAddress);
        Assert.Equal("https://api.example.test", ApiService.ExternalBaseAddress);
        Assert.Equal("https://api.example.test", RgfClientConfiguration.ExternalApiBaseAddress);
        Assert.Equal("https://api.example.test", RgfClientConfiguration.ProxyApiBaseAddress);
        Assert.Equal(RgfApiAuthMode.None, RgfClientConfiguration.ApiAuthMode);
    }

    [Fact]
    public void AddRgfServices_UsesServiceCollectionLoggerWhenLoggerIsNotProvided()
    {
        var services = CreateServiceCollection();
        var loggerProvider = new ListLoggerProvider();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        services.AddRgfServices(CreateConfiguration());

        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("AddRgfServices:", StringComparison.Ordinal));
    }

    [Fact]
    public void AddRgfServices_PrefersExplicitLoggerOverResolvedLogger()
    {
        var services = CreateServiceCollection();
        var loggerProvider = new ListLoggerProvider();
        var explicitLogger = new ListLogger();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        services.AddRgfServices(CreateConfiguration(), explicitLogger);

        Assert.NotEmpty(explicitLogger.Entries);
        Assert.Empty(loggerProvider.Entries);
    }

    [Fact]
    public void AddRgfServices_MissingBaseAddress_LogsCriticalMessageBeforeThrowing()
    {
        var services = CreateServiceCollection();
        var loggerProvider = new ListLoggerProvider();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRgfServices(CreateConfiguration(baseAddress: string.Empty)));

        Assert.Contains("BaseAddress", exception.Message, StringComparison.Ordinal);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Critical &&
            entry.Message.Contains("BaseAddress", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(RgfApiAuthMode.ServerProxy)]
    [InlineData(RgfApiAuthMode.ServerProxySsr)]
    public void AddRgfServices_MissingProxyBaseAddress_ForServerProxyModes_ThrowsAndLogs(RgfApiAuthMode authMode)
    {
        var services = CreateServiceCollection();
        var loggerProvider = new ListLoggerProvider();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRgfServices(CreateConfiguration(), authMode: authMode));

        Assert.Contains("proxy base address", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Critical &&
            entry.Message.Contains("proxy base address", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(RgfApiAuthMode.None)]
    [InlineData(RgfApiAuthMode.WasmBearer)]
    public void AddRgfServices_WithoutProxyConfiguration_UsesExternalBaseAddress_ForClientAuthModes(RgfApiAuthMode authMode)
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(CreateConfiguration(baseAddress: "https://api.example.test/"), authMode: authMode);

        Assert.Equal("https://api.example.test", RgfClientConfiguration.ExternalApiBaseAddress);
        Assert.Equal("https://api.example.test", RgfClientConfiguration.ProxyApiBaseAddress);
        Assert.Equal("https://api.example.test", ApiService.BaseAddress);
        Assert.Equal(authMode, RgfClientConfiguration.ApiAuthMode);
    }

    [Theory]
    [InlineData(RgfApiAuthMode.None)]
    [InlineData(RgfApiAuthMode.WasmBearer)]
    [InlineData(RgfApiAuthMode.ServerProxy)]
    [InlineData(RgfApiAuthMode.ServerProxySsr)]
    public void AddRgfServices_UsesConfiguredProxyBaseAddress_WhenProvided(RgfApiAuthMode authMode)
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(
            CreateConfiguration(proxyBaseAddress: "https://proxy.example.test/"),
            authMode: authMode);

        Assert.Equal("https://proxy.example.test", RgfClientConfiguration.ProxyApiBaseAddress);
        Assert.Equal("https://proxy.example.test", ApiService.BaseAddress);
        Assert.Equal("https://api.example.test", RgfClientConfiguration.ExternalApiBaseAddress);
    }

    [Fact]
    public void AddRgfServices_ProxyBaseAddressOverride_TakesPrecedenceOverConfiguration()
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(
            CreateConfiguration(proxyBaseAddress: "https://configured-proxy.example.test/"),
            authMode: RgfApiAuthMode.ServerProxy,
            proxyBaseAddressOverride: "https://override-proxy.example.test/");

        Assert.Equal("https://override-proxy.example.test", RgfClientConfiguration.ProxyApiBaseAddress);
        Assert.Equal("https://override-proxy.example.test", ApiService.BaseAddress);
    }

    [Fact]
    public void AddRgfServices_UsesAppRootPath_AndTrimsTrailingSlash()
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(CreateConfiguration(appRootPath: "/my/app/"));

        Assert.Equal("/my/app", RgfClientConfiguration.AppRootPath);
    }

    [Fact]
    public void AddRgfServices_FallsBackToAppRootUrl_WhenAppRootPathIsMissing()
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(CreateConfiguration(appRootUrl: "/legacy/root/"));

        Assert.Equal("/legacy/root", RgfClientConfiguration.AppRootPath);
    }

    [Fact]
    public void AddRgfServices_RegistersClientVersionHeaderOnlyOnceAcrossRepeatedCalls()
    {
        var services = CreateServiceCollection();

        services.AddRgfServices(CreateConfiguration());
        services.AddRgfServices(CreateConfiguration(baseAddress: "https://second.example.test"));

        Assert.Equal(1, RgfClientTestState.CountClientVersionHeaders());
        Assert.True(RgfClientConfiguration.ClientVersions.ContainsKey(RgfHeaderKeys.RgfClientVersion));
        Assert.Equal(RgfClientConfiguration.Version, RgfClientConfiguration.ClientVersions[RgfHeaderKeys.RgfClientVersion]);
    }

    [Fact]
    public async Task InitializeRgfClientAsync_WithClientSideRendering_InitializesLoggerFactoryAndDictionary()
    {
        var services = CreateServiceCollection();
        var recroDict = new FakeRecroDictService();

        services.AddLogging();
        services.AddSingleton<IRecroDictService>(recroDict);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => RgfLoggerFactory.GetRequiredLogger<RgfClientConfiguration>());

        await serviceProvider.InitializeRgfClientAsync();

        Assert.True(RgfClientConfiguration.IsInitialized);
        Assert.NotNull(RgfLoggerFactory.GetLogger<RgfClientConfiguration>());
        Assert.Equal(1, recroDict.InitializeCallCount);
        Assert.Null(recroDict.LastInitializeLanguage);
    }

    [Fact]
    public async Task InitializeRgfClientAsync_WithWasmBearer_UsesSecurityLanguageForDictionaryInitialization()
    {
        var services = CreateServiceCollection();
        var recroDict = new FakeRecroDictService();
        var recroSec = new FakeRecroSecService { UserLanguageValue = "hun" };

        services.AddLogging();
        services.AddRgfServices(CreateConfiguration(), authMode: RgfApiAuthMode.WasmBearer);
        services.AddSingleton<IRecroDictService>(recroDict);
        services.AddSingleton<IRecroSecService>(recroSec);

        using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.InitializeRgfClientAsync();

        Assert.Equal(1, recroDict.InitializeCallCount);
        Assert.Equal("hun", recroDict.LastInitializeLanguage);
    }

    [Fact]
    public async Task InitializeRgfClientAsync_WithoutClientSideRendering_DoesNotInitializeDictionary()
    {
        var services = CreateServiceCollection();
        var recroDict = new FakeRecroDictService();

        services.AddLogging();
        services.AddSingleton<IRecroDictService>(recroDict);

        using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.InitializeRgfClientAsync(clientSideRendering: false);

        Assert.True(RgfClientConfiguration.IsInitialized);
        Assert.Equal(0, recroDict.InitializeCallCount);
    }

    [Fact]
    public async Task InitializeRgfClientAsync_IsIdempotent()
    {
        var services = CreateServiceCollection();
        var recroDict = new FakeRecroDictService();
        var recroSec = new FakeRecroSecService { UserLanguageValue = "eng" };

        services.AddLogging();
        services.AddRgfServices(CreateConfiguration(), authMode: RgfApiAuthMode.WasmBearer);
        services.AddSingleton<IRecroDictService>(recroDict);
        services.AddSingleton<IRecroSecService>(recroSec);

        using var serviceProvider = services.BuildServiceProvider();

        await serviceProvider.InitializeRgfClientAsync();
        await serviceProvider.InitializeRgfClientAsync();

        Assert.Equal(1, recroDict.InitializeCallCount);
    }

    private static ServiceCollection CreateServiceCollection() => [];

    private static IConfiguration CreateConfiguration(
        string? baseAddress = "https://api.example.test/",
        string? proxyBaseAddress = null,
        string? appRootPath = null,
        string? appRootUrl = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = baseAddress,
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = proxyBaseAddress,
            ["Recrovit:RecroGridFramework:AppRootPath"] = appRootPath,
            ["Recrovit:RecroGridFramework:AppRootUrl"] = appRootUrl,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void AssertContainsDescriptor<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == lifetime);
    }

    private static void AssertContainsDescriptor<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.Lifetime == lifetime);
    }

    private sealed class FakeRecroDictService : IRecroDictService
    {
        public bool IsInitialized { get; private set; }

        public int InitializeCallCount { get; private set; }

        public string? LastInitializeLanguage { get; private set; }

        public Dictionary<string, string> Languages { get; } = [];

        public string DefaultLanguage { get; set; } = "eng";

        public Task InitializeAsync(string language = null!)
        {
            InitializeCallCount++;
            LastInitializeLanguage = language;
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<ConcurrentDictionary<string, string>> GetDictionaryAsync(string scope, string language = null!, bool authClient = true)
            => Task.FromResult(new ConcurrentDictionary<string, string>());

        public string GetRgfUiString(string resourceKey) => resourceKey;
    }

    private sealed class FakeRecroSecService : IRecroSecService
    {
        public EventDispatcher<EventArgs> AuthenticationStateChanged { get; } = new();

        public string? UserName => CurrentUser.Identity?.Name;

        public bool IsAuthenticated => CurrentUser.Identity?.IsAuthenticated == true;

        public bool IsAdmin { get; set; }

        public List<string> RoleClaim { get; } = [];

        public ClaimsPrincipal CurrentUser { get; set; } = new(new ClaimsIdentity());

        public Dictionary<string, string> Roles { get; } = [];

        public string UserLanguage => UserLanguageValue ?? "eng";

        public string? UserLanguageValue { get; set; }

        public EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; } = new();

        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);

        public Task<string?> SetUserLanguageAsync(string? language)
        {
            var previous = UserLanguageValue;
            UserLanguageValue = language;
            return Task.FromResult(previous);
        }

        public Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60)
            => throw new NotSupportedException();

        public Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60)
            => throw new NotSupportedException();

        public Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60)
            => throw new NotSupportedException();
    }
}
