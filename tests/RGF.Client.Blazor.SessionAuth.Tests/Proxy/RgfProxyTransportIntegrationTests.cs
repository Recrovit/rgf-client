using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Authentication;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Client.Services;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class RgfProxyTransportIntegrationTests
{
    [Fact]
    public async Task ServerProgressClientSendsHostOriginThroughNegotiateAndWebSocketToSeparateApi()
    {
        await using var fixture = await ProxyHost.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = fixture.Origin,
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = fixture.ApiOrigin
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var progress = provider.GetRequiredService<IRgfProgressService>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.NotNull(await progress.StartAsync(cancellationToken: timeout.Token));
        Assert.Contains(fixture.HostRequests, r => !r.WebSocket && r.Path.EndsWith("/negotiate", StringComparison.Ordinal));
        Assert.Contains(fixture.HostRequests, r => r.WebSocket);
        Assert.All(fixture.HostRequests, r => Assert.Equal(fixture.Origin, r.Origin));
        Assert.Contains(fixture.ApiRequests, r => r.WebSocket);
        Assert.All(fixture.ApiRequests, r =>
        {
            Assert.Equal("Bearer test-token", r.Authorization);
            Assert.Equal(string.Empty, r.Cookie);
        });
        await progress.StopAsync(timeout.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserLikeNegotiateAndWebSocketAcceptHostOrWhitelistedOrigin(bool whitelisted)
    {
        await using var fixture = await ProxyHost.StartAsync();
        await using var connection = new HubConnectionBuilder().WithUrl($"{fixture.Origin}/rgf/progressHub", options =>
        {
            options.Headers["Origin"] = whitelisted ? "https://trusted.example.test" : fixture.Origin;
        }).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.StartAsync(timeout.Token);
        Assert.NotNull(connection.ConnectionId);
        Assert.Contains(fixture.ApiRequests, r => r.WebSocket);
        await connection.StopAsync(timeout.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("https://foreign.example.test")]
    public async Task RejectedHandshakeDoesNotOpenApiSocketOrAcquireToken(string? origin)
    {
        await using var fixture = await ProxyHost.StartAsync();
        using var socket = new ClientWebSocket();
        if (origin is not null) socket.Options.SetRequestHeader("Origin", origin);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var exception = await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(
            new Uri(fixture.Origin.Replace("http://", "ws://", StringComparison.Ordinal) + "/rgf/progressHub"), timeout.Token));
        Assert.Contains("403", exception.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.ApiRequests);
        Assert.Equal(0, fixture.Tokens.Calls);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task HttpRejectionPrecedesApiAndTokenAcquisitionAndAuthorizationIsPreserved(string method)
    {
        await using var fixture = await ProxyHost.StartAsync();
        using var client = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var rejected = new HttpRequestMessage(new HttpMethod(method), fixture.Origin + "/rgf/api/test");
        rejected.Headers.Add("Origin", fixture.Origin);
        rejected.Headers.Add("Sec-Fetch-Site", "cross-site");
        using var denied = await client.SendAsync(rejected, timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(fixture.ApiRequests);
        Assert.Equal(0, fixture.Tokens.Calls);

        using var anonymous = new HttpRequestMessage(new HttpMethod(method), fixture.Origin + "/rgf/api/test");
        anonymous.Headers.Add("Origin", fixture.Origin);
        anonymous.Headers.Add("X-Test-Anonymous", "true");
        using var unauthorized = await client.SendAsync(anonymous, timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Empty(fixture.ApiRequests);
        Assert.Equal(0, fixture.Tokens.Calls);

        using var accepted = new HttpRequestMessage(new HttpMethod(method), fixture.Origin + "/rgf/api/test");
        accepted.Headers.Add("Origin", fixture.Origin);
        using var response = await client.SendAsync(accepted, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, fixture.Tokens.Calls);
        Assert.Equal("Bearer test-token", Assert.Single(fixture.ApiRequests).Authorization);
    }

    public sealed class ProgressHub : Hub { }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task SsrHostOriginMatchesTrustedForwardedHeadersWithSeparateDownstream(string method)
    {
        await using var fixture = await ProxyHost.StartAsync();
        const string publicOrigin = "https://public.example.test";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRgfBlazorSessionAuthSsrServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:ProxyBaseAddress"] = publicOrigin,
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = fixture.ApiOrigin
        }).Build());
        services.AddScoped<IRgfServerRequestCookieAccessor, CookieAccessor>();
        services.AddHttpClient(ApiService.RgfApiClientName).ConfigurePrimaryHttpMessageHandler(() =>
            new LoopbackGatewayHandler(fixture.Origin) { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false } });
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ApiService.RgfApiClientName);
        using var request = new HttpRequestMessage(new HttpMethod(method), "/rgf/api/test");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(publicOrigin, Assert.Single(fixture.HostRequests).Origin);
        var downstream = Assert.Single(fixture.ApiRequests);
        Assert.Equal(string.Empty, downstream.Cookie);
        Assert.Equal("Bearer test-token", downstream.Authorization);
    }

    [Fact]
    public async Task UnconfiguredDotNetSignalRClientHasNoBrowserSignalAndIsRejected()
    {
        await using var fixture = await ProxyHost.StartAsync();
        await using var connection = new HubConnectionBuilder().WithUrl(fixture.Origin + "/rgf/progressHub").Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync(timeout.Token));
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.All(fixture.HostRequests, r => Assert.Equal(string.Empty, r.Origin));
        Assert.Empty(fixture.ApiRequests);
        Assert.Equal(0, fixture.Tokens.Calls);
    }

    private sealed class CookieAccessor : IRgfServerRequestCookieAccessor
    {
        public string GetCookieHeader() => "session=ssr-test";
    }

    // Simulate a gateway routing the configured public Host to its internal loopback listener.
    private sealed class LoopbackGatewayHandler(string destination) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var publicUri = request.RequestUri!;
            request.Headers.Add("X-Forwarded-Host", publicUri.Authority);
            request.Headers.Add("X-Forwarded-Proto", publicUri.Scheme);
            request.RequestUri = new Uri(destination + publicUri.PathAndQuery);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed record CapturedRequest(string Path, bool WebSocket, string Origin, string Authorization, string Cookie);

    private sealed class TokenProvider : IDownstreamUserTokenProvider
    {
        public int Calls;
        public Task<string> GetAccessTokenAsync(ClaimsPrincipal user, string downstreamApiName, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult("test-token");
        }
    }

    private sealed class ProxyHost(WebApplication host, WebApplication api, TokenProvider tokens,
        ConcurrentQueue<CapturedRequest> hostRequests, ConcurrentQueue<CapturedRequest> apiRequests) : IAsyncDisposable
    {
        public string Origin => host.Urls.Single();
        public string ApiOrigin => api.Urls.Single();
        public TokenProvider Tokens => tokens;
        public ConcurrentQueue<CapturedRequest> HostRequests => hostRequests;
        public ConcurrentQueue<CapturedRequest> ApiRequests => apiRequests;

        public static async Task<ProxyHost> StartAsync()
        {
            var apiBuilder = CreateBuilder();
            apiBuilder.Services.AddSignalR();
            var api = apiBuilder.Build();
            var apiRequests = new ConcurrentQueue<CapturedRequest>();
            api.UseWebSockets();
            Capture(api, apiRequests);
            api.MapHub<ProgressHub>("/rgf/progressHub");
            api.MapMethods("/rgf/api/{**path}", ["GET", "POST"], () => Results.Ok());
            await api.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                var builder = CreateBuilder();
                ProxyProtectionTestServices.Add(builder);
                builder.Configuration["Recrovit:OpenIdConnect:Host:DownstreamProxyRequestProtection:AllowedWebSocketOrigins:0"] = "https://trusted.example.test";
                builder.Configuration["Recrovit:OpenIdConnect:Host:DownstreamProxyRequestProtection:AllowedHttpOrigins:0"] = "https://trusted.example.test";
                builder.Services.AddSingleton(new DownstreamApiCatalog(new Dictionary<string, DownstreamApiDefinition>
                {
                    ["RgfApi"] = new() { BaseUrl = api.Urls.Single(), Scopes = ["api.scope"] }
                }));
                var tokens = new TokenProvider();
                builder.Services.AddSingleton<IDownstreamUserTokenProvider>(tokens);
                var host = builder.Build();
                host.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
                });
                host.UseWebSockets();
                host.UseAuthentication();
                host.Use((context, next) =>
                {
                    if (!context.Request.Headers.ContainsKey("X-Test-Anonymous"))
                        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "test"));
                    return next(context);
                });
                host.UseAuthorization();
                var hostRequests = new ConcurrentQueue<CapturedRequest>();
                Capture(host, hostRequests);
                host.MapRgfProxyEndpoints();
                await host.StartAsync(TestContext.Current.CancellationToken);
                return new(host, api, tokens, hostRequests, apiRequests);
            }
            catch
            {
                await api.DisposeAsync();
                throw;
            }
        }

        private static WebApplicationBuilder CreateBuilder()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            return builder;
        }

        private static void Capture(WebApplication app, ConcurrentQueue<CapturedRequest> requests) => app.Use((context, next) =>
        {
            requests.Enqueue(new(context.Request.Path, context.WebSockets.IsWebSocketRequest, context.Request.Headers.Origin.ToString(),
                context.Request.Headers.Authorization.ToString(), context.Request.Headers.Cookie.ToString()));
            return next(context);
        });

        public async ValueTask DisposeAsync()
        {
            await host.DisposeAsync();
            await api.DisposeAsync();
        }
    }
}
