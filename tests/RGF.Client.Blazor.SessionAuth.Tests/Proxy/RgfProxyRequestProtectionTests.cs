using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class RgfProxyRequestProtectionTests
{
    public static TheoryData<string, string, bool> Routes => new()
    {
        { "/RGF/base/{**path}", "GET", true }, { "/RGF/base/{**path}", "POST", true },
        { "/RGF/RecroGrid/{**path}", "GET", true }, { "/RGF/RecroGrid/{**path}", "POST", true },
        { "/rgf/api/{**path}", "GET", true }, { "/rgf/api/{**path}", "POST", true },
        { "/api/rgf/ai/recroby/catalog", "GET", true }, { "/api/rgf/ai/recroby", "POST", true },
        { "/rgf/progressHub", "GET", false }, { "/rgf/progressHub/{**path}", "POST", false }
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryProtectedRouteHasMetadataAndRejectsBeforeForwarding(string route, string method, bool authorized)
    {
        await using var app = CreateApp();
        var endpoint = FindEndpoint(app, route);
        Assert.NotNull(endpoint.Metadata.GetMetadata<ProxyEndpointMetadata>());
        Assert.Equal(authorized, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0);
        var proxy = app.Services.GetRequiredService<RecordingProxy>();
        var context = CreateContext(app, endpoint, method);
        context.Request.Headers.Origin = "https://foreign.example.test";
        await endpoint.RequestDelegate!(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal(0, proxy.HttpCalls + proxy.WebSocketCalls);

        context = CreateContext(app, endpoint, method);
        context.Request.Headers.Origin = "https://host.example.test";
        await endpoint.RequestDelegate!(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(1, proxy.HttpCalls);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("cross-site", "https://trusted.example.test", false)]
    [InlineData("same-site", "https://host.example.test", false)]
    [InlineData("same-origin", "https://trusted.example.test", false)]
    [InlineData("bogus", "https://host.example.test", false)]
    [InlineData("", "https://host.example.test", false)]
    [InlineData(null, "null", false)]
    [InlineData(null, "https://host.example.test/path", false)]
    [InlineData(null, "https://host.example.test,https://trusted.example.test", false)]
    [InlineData("same-origin", "https://host.example.test", true)]
    [InlineData("same-origin", null, true)]
    [InlineData(null, "https://trusted.example.test", true)]
    public async Task HttpSignalsUseSharedPolicyWithoutAntiforgery(string? fetch, string? origin, bool allowed)
    {
        await using var app = CreateApp(new() { AllowedHttpOrigins = ["https://trusted.example.test"] });
        var endpoint = FindEndpoint(app, "/api/rgf/ai/recroby");
        var context = CreateContext(app, endpoint, "POST");
        if (fetch is not null) context.Request.Headers["Sec-Fetch-Site"] = fetch;
        if (origin is not null) context.Request.Headers.Origin = origin;
        await endpoint.RequestDelegate!(context);
        Assert.Equal(allowed ? 200 : 403, context.Response.StatusCode);
        Assert.Equal(allowed ? 1 : 0, app.Services.GetRequiredService<RecordingProxy>().HttpCalls);
    }

    [Theory]
    [InlineData("https://host.example.test", false, true)]
    [InlineData("https://trusted.example.test", false, true)]
    [InlineData("https://foreign.example.test", false, false)]
    [InlineData("https://http-only.example.test", false, false)]
    [InlineData("null", true, false)]
    [InlineData("https://host.example.test/path", true, false)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    public async Task WebSocketPolicyRunsBeforeOpeningDownstream(string? origin, bool allowMissing, bool allowed)
    {
        await using var app = CreateApp(new() { AllowedHttpOrigins = ["https://http-only.example.test"], AllowedWebSocketOrigins = ["https://trusted.example.test"], AllowMissingWebSocketOrigin = allowMissing });
        foreach (var route in new[] { "/rgf/progressHub", "/rgf/progressHub/{**path}" })
        {
            var endpoint = FindEndpoint(app, route);
            var context = CreateContext(app, endpoint, "GET");
            context.Features.Set<IHttpWebSocketFeature>(new WebSocketFeature());
            if (origin is not null) context.Request.Headers.Origin = origin;
            await endpoint.RequestDelegate!(context);
            Assert.Equal(allowed ? 200 : 403, context.Response.StatusCode);
        }
        var proxy = app.Services.GetRequiredService<RecordingProxy>();
        Assert.Equal(allowed ? 2 : 0, proxy.WebSocketCalls);
        Assert.Equal(0, proxy.HttpCalls);
    }

    [Theory]
    [InlineData("/rgf/api/RGFSriptReferences/{**suffix}")]
    [InlineData("/rgf/api/RGFStylesheetsReferences")]
    [InlineData("/rgf/api/RecroDict/{**path}")]
    [InlineData("/rgf/styles.{environment}/{**path}")]
    [InlineData("/rgf/resource/{**path}")]
    [InlineData("/api/rgf/capabilities")]
    public async Task PublicGetStillForwardsWithoutBrowserSignalsOrUserToken(string route)
    {
        await using var app = CreateApp();
        var endpoint = FindEndpoint(app, route);
        Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        await endpoint.RequestDelegate!(CreateContext(app, endpoint, "GET"));
        var proxy = app.Services.GetRequiredService<RecordingProxy>();
        Assert.Equal(1, proxy.HttpCalls);
        Assert.Null(proxy.User);
    }

    private static WebApplication CreateApp(DownstreamProxyRequestProtectionOptions? options = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        ProxyProtectionTestServices.Add(builder);
        // Options use init-only properties, so supply the complete policy through IOptions.
        builder.Services.AddSingleton<Microsoft.Extensions.Options.IOptions<OidcAuthenticationOptions>>(
            Microsoft.Extensions.Options.Options.Create(new OidcAuthenticationOptions { DownstreamProxyRequestProtection = options ?? new() }));
        builder.Services.AddSingleton<RecordingProxy>();
        builder.Services.AddSingleton<IDownstreamHttpProxyClient>(sp => sp.GetRequiredService<RecordingProxy>());
        builder.Services.AddSingleton<IDownstreamTransportProxyClient>(sp => sp.GetRequiredService<RecordingProxy>());
        var app = builder.Build();
        app.MapRgfProxyEndpoints();
        return app;
    }

    [Fact]
    public async Task MissingProxyMetadataFailsClosedAtRgfIntegration()
    {
        await using var app = CreateApp();
        var endpoint = FindEndpoint(app, "/api/rgf/ai/recroby");
        var context = CreateContext(app, new Endpoint(endpoint.RequestDelegate, new EndpointMetadataCollection(), "without metadata"), "POST");
        context.Request.Headers.Origin = "https://host.example.test";
        await endpoint.RequestDelegate!(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal(0, app.Services.GetRequiredService<RecordingProxy>().HttpCalls);
    }

    [Theory]
    [InlineData("Origin")]
    [InlineData("Sec-Fetch-Site")]
    public async Task MultipleHeaderValuesAreRejected(string header)
    {
        await using var app = CreateApp();
        var endpoint = FindEndpoint(app, "/api/rgf/ai/recroby");
        var context = CreateContext(app, endpoint, "POST");
        context.Request.Headers.Origin = "https://host.example.test";
        context.Request.Headers[header] = new StringValues(["same-origin", "same-origin"]);
        await endpoint.RequestDelegate!(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Equal(0, app.Services.GetRequiredService<RecordingProxy>().HttpCalls);
    }

    [Fact]
    public async Task ExplicitSameSiteOptInUsesExistingWhitelist()
    {
        await using var app = CreateApp(new() { AllowSameSite = true, AllowedHttpOrigins = ["https://trusted.example.test"] });
        var endpoint = FindEndpoint(app, "/api/rgf/ai/recroby");
        var context = CreateContext(app, endpoint, "POST");
        context.Request.Headers.Origin = "https://trusted.example.test";
        context.Request.Headers["Sec-Fetch-Site"] = "same-site";
        await endpoint.RequestDelegate!(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<RecordingProxy>().HttpCalls);
    }

    private static RouteEndpoint FindEndpoint(WebApplication app, string route) => Assert.Single(
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>(), e => e.RoutePattern.RawText == route);

    private static DefaultHttpContext CreateContext(WebApplication app, Endpoint endpoint, string method)
    {
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.SetEndpoint(endpoint);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("host.example.test");
        context.Request.Path = "/rgf/api/test";
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class WebSocketFeature : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy : IDownstreamHttpProxyClient, IDownstreamTransportProxyClient
    {
        public int HttpCalls { get; private set; }
        public int WebSocketCalls { get; private set; }
        public ClaimsPrincipal? User { get; private set; }
        public Task<HttpResponseMessage> SendAsync(string downstreamApiName, HttpMethod method, string pathAndQuery,
            ClaimsPrincipal? user, HttpContent? content, IEnumerable<KeyValuePair<string, StringValues>> headers, CancellationToken cancellationToken)
        {
            HttpCalls++;
            User = user;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
        public Task ProxyWebSocketAsync(HttpContext context, string downstreamApiName, string pathAndQuery, ClaimsPrincipal? user, CancellationToken cancellationToken)
        {
            WebSocketCalls++;
            return Task.CompletedTask;
        }
    }
}
