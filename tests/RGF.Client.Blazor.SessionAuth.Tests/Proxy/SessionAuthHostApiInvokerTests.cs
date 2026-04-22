using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class SessionAuthHostApiInvokerTests
{
    [Fact]
    public async Task SendAsync_UsesConfiguredHostApiRoute()
    {
        var handler = new CaptureRequestHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthClientServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"), "https://host.example.test");
        services.AddHttpClient(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IHostApiInvoker>();

        using var response = await invoker.SendAsync(new HostApiRequest
        {
            Path = "api/userinfo?details=true",
            Method = HttpMethod.Get
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://host.example.test/api/userinfo?details=true", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAsync_PreservesAbsoluteHostPath()
    {
        var handler = new CaptureRequestHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthClientServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"), "https://host.example.test");
        services.AddHttpClient(RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IHostApiInvoker>();

        using var response = await invoker.GetAsync("/api/userinfo", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://host.example.test/api/userinfo", handler.LastRequest!.RequestUri!.ToString());
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

    private sealed class CaptureRequestHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty)
            });
        }
    }
}
