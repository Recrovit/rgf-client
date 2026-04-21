using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Configuration;

public sealed class RgfBlazorSessionAuthConfigurationTests
{
    [Fact]
    public void AddRgfBlazorSessionAuthClientServices_ShouldConfigureClientFoundContentByDefault()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddRecrovitComponentRouting(options => options.AddRouteAssembly(typeof(PublicInteractiveAutoPage).Assembly));
        services.AddRgfBlazorSessionAuthClientServices(CreateConfiguration(), apiBaseAddressOverride: "https://host.example.test");

        using var serviceProvider = services.BuildServiceProvider();
        var routingOptions = serviceProvider.GetRequiredService<IOptions<RecrovitRoutingOptions>>().Value;

        Assert.NotNull(routingOptions.GetFoundContent(RecrovitRoutesKind.Client));
        Assert.Null(routingOptions.GetFoundContent(RecrovitRoutesKind.Host));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRgfAuthenticationSessionMonitor));
    }

    [Fact]
    public void AddRgfBlazorSessionAuthSsrServices_AttachesCookieForwardingHandlerToConfiguredClients()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"));

        using var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

        Assert.Equal(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Services.NoOpRgfAuthenticationSessionMonitor",
            serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>().GetType().FullName);
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(ApiService.RgfApiClientName), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(ApiService.RgfAuthApiClientName), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get("Recrovit.RGF.Blazor.SessionAuth.ServerProxy"), serviceProvider));
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

    private static IConfiguration CreateConfiguration(string? baseAddress = "https://api.example.test", string? proxyBaseAddress = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = baseAddress,
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = proxyBaseAddress,
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
}
