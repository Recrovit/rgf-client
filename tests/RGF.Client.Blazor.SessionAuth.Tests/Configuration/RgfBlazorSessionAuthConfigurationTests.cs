using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Configuration;

public sealed class RgfBlazorSessionAuthConfigurationTests
{
    public static IEnumerable<object[]> SsrTransports()
    {
        foreach (var name in new[] { ApiService.RgfApiClientName, ApiService.RgfAuthApiClientName,
            "Recrovit.RGF.Blazor.SessionAuth.ServerProxy", RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName,
            RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName })
        {
            foreach (var kind in new[] { "sockets", "client", "wrapped-sockets", "wrapped-client" })
                yield return [name, kind];
        }
    }

    [Theory]
    [MemberData(nameof(SsrTransports))]
    public void SsrClientsSecureExistingTransportAfterLaterNormalRegistration(string name, string kind)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"));
        using HttpMessageHandler transport = kind.EndsWith("sockets", StringComparison.Ordinal)
            ? new SocketsHttpHandler { UseProxy = false, MaxConnectionsPerServer = 7, ConnectTimeout = TimeSpan.FromSeconds(9) }
            : new HttpClientHandler { UseProxy = false, MaxConnectionsPerServer = 7 };
        using HttpMessageHandler primary = kind.StartsWith("wrapped", StringComparison.Ordinal)
            ? new PassthroughHandler { InnerHandler = new PassthroughHandler { InnerHandler = transport } }
            : transport;
        services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => primary);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);

        if (transport is SocketsHttpHandler sockets)
        {
            Assert.False(sockets.AllowAutoRedirect);
            Assert.False(sockets.UseCookies);
            Assert.False(sockets.UseProxy);
            Assert.Equal(7, sockets.MaxConnectionsPerServer);
            Assert.Equal(TimeSpan.FromSeconds(9), sockets.ConnectTimeout);
        }
        else
        {
            var handler = Assert.IsType<HttpClientHandler>(transport);
            Assert.False(handler.AllowAutoRedirect);
            Assert.False(handler.UseCookies);
            Assert.False(handler.UseProxy);
            Assert.Equal(7, handler.MaxConnectionsPerServer);
        }
        if (primary is DelegatingHandler wrapper)
            Assert.Same(transport, Assert.IsType<PassthroughHandler>(wrapper.InnerHandler).InnerHandler);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SsrClientRejectsUninspectableTransport(bool missingInnerHandler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"));
        services.AddHttpClient(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName)
            .ConfigurePrimaryHttpMessageHandler(() => missingInnerHandler ? new PassthroughHandler() : new OpaqueHandler());
        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName));
        Assert.Contains(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName, exception.Message);
        Assert.Contains("requires an inspectable", exception.Message);
    }

    private sealed class PassthroughHandler : DelegatingHandler { }

    private sealed class OpaqueHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

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
        Assert.NotNull(serviceProvider.GetService<IHostApiInvoker>());
        Assert.NotNull(serviceProvider.GetService<IDownstreamApiInvoker>());
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
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session.NoOpRgfAuthenticationSessionMonitor",
            serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>().GetType().FullName);
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(ApiService.RgfApiClientName), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(ApiService.RgfAuthApiClientName), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get("Recrovit.RGF.Blazor.SessionAuth.ServerProxy"), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName), serviceProvider));
        Assert.Equal(typeof(RgfServerProxyAuthCookieHandler), ResolveConfiguredHandlerType(optionsMonitor.Get(RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName), serviceProvider));
        Assert.NotNull(serviceProvider.GetService<IHostApiInvoker>());
        Assert.NotNull(serviceProvider.GetService<IDownstreamApiInvoker>());
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
