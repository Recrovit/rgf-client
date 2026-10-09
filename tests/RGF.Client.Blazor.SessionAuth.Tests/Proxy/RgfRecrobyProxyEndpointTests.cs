using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Proxy;

public sealed class RgfRecrobyProxyEndpointTests
{
    [Theory]
    [InlineData("/api/rgf/ai/recroby", "POST", true)]
    [InlineData("/api/rgf/ai/recroby/catalog", "GET", true)]
    [InlineData("/api/rgf/capabilities", "GET", false)]
    public async Task RecrobyAndCapabilitiesUseBackendProxyWithAppropriateAuthorization(string path, string method, bool authorized)
    {
        var proxy = new RecordingProxy();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSingleton<IDownstreamHttpProxyClient>(proxy);
        builder.Services.AddSingleton(DispatchProxy.Create<IDownstreamTransportProxyClient, UnusedTransportProxy>());
        builder.Services.AddSingleton(new DownstreamApiCatalog(new Dictionary<string, DownstreamApiDefinition>
        { ["RgfApi"] = new() { BaseUrl = "https://api.example.test" } }));
        await using var app = builder.Build();
        app.MapRgfProxyEndpoints();
        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>(), item => item.RoutePattern.RawText == path);
        Assert.Equal(authorized, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0);
        Assert.Equal(!authorized, endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
        Assert.Equal(new[] { method }, endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "owner")], "test"));
        var context = new DefaultHttpContext { RequestServices = app.Services, User = user };
        context.SetEndpoint(endpoint);
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers[RgfHeaderKeys.RgfClientVersion] = "client-version";
        context.Request.Headers[RgfHeaderKeys.RgfClientBlazorVersion] = "blazor-version";
        context.Request.Headers["RGF-SessionId"] = "session-id";
        context.Request.Headers["X-Unapproved"] = "must-not-forward";
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);

        Assert.Equal("RgfApi", proxy.Downstream);
        Assert.Equal(new HttpMethod(method), proxy.Method);
        Assert.Equal(path, proxy.Path);
        if (authorized) Assert.Same(user, proxy.User);
        else Assert.Null(proxy.User);
        Assert.Equal("client-version", proxy.Headers[RgfHeaderKeys.RgfClientVersion].ToString());
        Assert.Equal("blazor-version", proxy.Headers[RgfHeaderKeys.RgfClientBlazorVersion].ToString());
        Assert.Equal("session-id", proxy.Headers["RGF-SessionId"].ToString());
        Assert.False(proxy.Headers.ContainsKey("X-Unapproved"));
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private sealed class RecordingProxy : IDownstreamHttpProxyClient
    {
        public string? Downstream { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public ClaimsPrincipal? User { get; private set; }
        public Dictionary<string, StringValues> Headers { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<HttpResponseMessage> SendAsync(string downstreamApiName, HttpMethod method, string pathAndQuery,
            ClaimsPrincipal? user, HttpContent? content, IEnumerable<KeyValuePair<string, StringValues>> headers,
            CancellationToken cancellationToken)
        {
            Downstream = downstreamApiName;
            Method = method;
            Path = pathAndQuery;
            User = user;
            Headers = headers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    private class UnusedTransportProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }
}
