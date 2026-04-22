using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Recrovit.RecroGridFramework.Client.Blazor.Handlers;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Configuration;

public sealed class RgfBlazorWasmBearerConfigurationTests
{
    [Fact]
    public void AddRgfBlazorWasmBearerServices_RegistersWasmAccessTokenAccessor()
    {
        var services = new ServiceCollection();

        services.AddRgfBlazorWasmBearerServices(CreateConfiguration());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRgfAccessTokenAccessor)
            && descriptor.ImplementationType == typeof(WasmRgfAccessTokenAccessor));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RgfAuthenticationEndpointResolver));
    }

    [Fact]
    public void AddRgfBlazorWasmBearerServices_DefaultsToRgfAuthorizationMessageHandler()
    {
        var services = CreateServicesWithDefaultHandlerDependencies();

        services.AddRgfBlazorWasmBearerServices(CreateConfiguration());

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(RgfAuthorizationMessageHandler)
            && descriptor.ImplementationType == typeof(RgfAuthorizationMessageHandler));

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(ApiService.RgfAuthApiClientName);

        Assert.NotEmpty(options.HttpMessageHandlerBuilderActions);
    }

    [Fact]
    public void AddRgfBlazorWasmBearerServices_UsesProvidedAuthorizationHandlerType()
    {
        var services = CreateServicesWithDefaultHandlerDependencies();
        services.AddTransient<TestAuthorizationMessageHandler>();

        services.AddRgfBlazorWasmBearerServices(CreateConfiguration(), authorizationMessageHandlerType: typeof(TestAuthorizationMessageHandler));

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(RgfAuthorizationMessageHandler)
            && descriptor.ImplementationType == typeof(RgfAuthorizationMessageHandler));

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(ApiService.RgfAuthApiClientName);

        Assert.Equal(typeof(TestAuthorizationMessageHandler), ResolveConfiguredHandlerType(options, serviceProvider));
    }

    private static ServiceCollection CreateServicesWithDefaultHandlerDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider, StubAccessTokenProvider>();
        services.AddSingleton<Microsoft.AspNetCore.Components.NavigationManager, StubNavigationManager>();
        services.AddSingleton<RgfAuthenticationEndpointResolver>();
        services.AddSingleton<IConfiguration>(CreateConfiguration());
        return services;
    }

    private static Type ResolveConfiguredHandlerType(HttpClientFactoryOptions options, IServiceProvider serviceProvider)
    {
        var builder = new TestHttpMessageHandlerBuilder(serviceProvider);
        foreach (var action in options.HttpMessageHandlerBuilderActions)
        {
            action(builder);
        }

        var handler = Assert.Single(builder.AdditionalHandlers);
        return handler.GetType();
    }

    private static IConfiguration CreateConfiguration(string? baseAddress = "https://api.example.test")
    {
        ApiService.BaseAddress = baseAddress ?? string.Empty;

        var values = new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = baseAddress,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHttpMessageHandlerBuilder(IServiceProvider services) : HttpMessageHandlerBuilder
    {
        public override string Name { get; set; } = string.Empty;

        public override HttpMessageHandler PrimaryHandler { get; set; } = new HttpClientHandler();

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override IServiceProvider Services { get; } = services;

        public override HttpMessageHandler Build() => PrimaryHandler;
    }

    private sealed class StubAccessTokenProvider : Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider
    {
        public ValueTask<Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult> RequestAccessToken()
            => ValueTask.FromResult(default(Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult)!);

        public ValueTask<Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult> RequestAccessToken(Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenRequestOptions options)
            => ValueTask.FromResult(default(Microsoft.AspNetCore.Components.WebAssembly.Authentication.AccessTokenResult)!);
    }

    private sealed class StubNavigationManager : Microsoft.AspNetCore.Components.NavigationManager
    {
        public StubNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class TestAuthorizationMessageHandler : DelegatingHandler;
}
