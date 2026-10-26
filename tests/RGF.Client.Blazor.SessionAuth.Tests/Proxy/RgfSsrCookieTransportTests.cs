using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class RgfSsrCookieTransportTests
{
    private static readonly string[] ClientNames = [ApiService.RgfApiClientName, ApiService.RgfAuthApiClientName,
        "Recrovit.RGF.Blazor.SessionAuth.ServerProxy", RgfBlazorSessionAuthConfigurationExtensions.HostApiClientName,
        RgfBlazorSessionAuthConfigurationExtensions.DownstreamProxyClientName];

    public static IEnumerable<object[]> RedirectCases()
    {
        foreach (var name in ClientNames)
            foreach (var status in new[] { 301, 302, 303, 307, 308 })
                foreach (var external in new[] { false, true })
                    yield return [name, status, external];
    }

    [Theory]
    [MemberData(nameof(RedirectCases))]
    public async Task RedirectReturnsOriginalResponseWithoutSecondRequest(string name, int status, bool external)
    {
        var externalRequests = new ConcurrentQueue<string>();
        await using var destination = await StartServerAsync(context =>
        {
            externalRequests.Enqueue(context.Request.Headers.Cookie.ToString());
            return Task.CompletedTask;
        });
        var hostRequests = new ConcurrentQueue<string>();
        Uri? location = null;
        await using var host = await StartServerAsync(context =>
        {
            hostRequests.Enqueue(context.Request.Headers.Cookie.ToString());
            if (context.Request.Path == "/start")
            {
                context.Response.StatusCode = status;
                context.Response.Headers.Location = location!.AbsoluteUri;
            }
            return Task.CompletedTask;
        });
        var origin = host.Urls.Single();
        location = new Uri((external ? destination.Urls.Single() : origin) + "/target");
        var cookies = new RequestCookies { Cookie = "session=user-a" };
        await using var provider = CreateServices(origin, cookies).BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        using var response = await client.PostAsync(origin + "/start", new StringContent("payload"), TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal(location, response.Headers.Location);
        Assert.Equal("session=user-a", Assert.Single(hostRequests));
        Assert.Empty(externalRequests);
    }

    public static IEnumerable<object[]> CookieCases()
    {
        foreach (var name in ClientNames)
            foreach (var sockets in new[] { false, true })
                yield return [name, sockets];
    }

    [Theory]
    [MemberData(nameof(CookieCases))]
    public async Task SharedTransportDoesNotStoreOrReplayCookiesBetweenSsrUsers(string name, bool sockets)
    {
        var requests = new ConcurrentQueue<string>();
        await using var host = await StartServerAsync(context =>
        {
            requests.Enqueue(context.Request.Headers.Cookie.ToString());
            context.Response.Headers.Append("Set-Cookie", "stored=user-a; Path=/");
            return Task.CompletedTask;
        });
        var origin = host.Urls.Single();
        var container = new CookieContainer();
        container.Add(new Uri(origin), new Cookie("preexisting", "other-user"));
        var cookies = new RequestCookies();
        var services = CreateServices(origin, cookies);
        using HttpMessageHandler transport = sockets
            ? new SocketsHttpHandler { CookieContainer = container }
            : new HttpClientHandler { CookieContainer = container };
        var creations = 0;
        services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() =>
        {
            creations++;
            return transport;
        });
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        foreach (var cookie in new[] { "session=user-a", "session=user-b", "" })
        {
            cookies.Cookie = cookie;
            using var client = factory.CreateClient(name);
            using var response = await client.GetAsync(origin + "/cookie", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(1, creations);
        Assert.Equal(new[] { "session=user-a", "session=user-b", "" }, requests.ToArray());
        Assert.Null(container.GetCookies(new Uri(origin))["stored"]);
        Assert.Equal("other-user", container.GetCookies(new Uri(origin))["preexisting"]!.Value);
    }

    private static ServiceCollection CreateServices(string origin, RequestCookies cookies)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = origin,
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://api.example.test"
        }).Build());
        services.AddSingleton<IRgfServerRequestCookieAccessor>(cookies);
        return services;
    }

    // Like an HTTP-context accessor, expose the current request independently of the pooled handler scope.
    private sealed class RequestCookies : IRgfServerRequestCookieAccessor
    {
        private readonly AsyncLocal<string?> current = new();
        public string Cookie { get => current.Value ?? ""; set => current.Value = value; }
        public string GetCookieHeader() => Cookie;
    }

    private static async Task<WebApplication> StartServerAsync(RequestDelegate handler)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Run(handler);
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }
}
