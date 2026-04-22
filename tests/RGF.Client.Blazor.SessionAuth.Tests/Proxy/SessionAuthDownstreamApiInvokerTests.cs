using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class SessionAuthDownstreamApiInvokerTests
{
    [Fact]
    public async Task SendAsync_UsesConfiguredGenericDownstreamProxyRoute()
    {
        var handler = new CaptureRequestHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthClientServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"), "https://host.example.test");
        services.AddHttpClient(RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IDownstreamApiInvoker>();

        using var response = await invoker.SendAsync(new DownstreamApiRequest
        {
            ApiName = "UserInfoApi",
            RelativePath = "connect/userinfo?lang=hu",
            Method = HttpMethod.Get
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://host.example.test/downstream/UserInfoApi/connect/userinfo?lang=hu", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAsync_UsesApiRootRoute_WhenRelativePathIsOmitted()
    {
        var handler = new CaptureRequestHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthClientServices(CreateConfiguration(proxyBaseAddress: "https://host.example.test"), "https://host.example.test");
        services.AddHttpClient(RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IDownstreamApiInvoker>();

        using var response = await invoker.GetAsync("UserInfoApi", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://host.example.test/downstream/UserInfoApi", handler.LastRequest!.RequestUri!.ToString());
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
