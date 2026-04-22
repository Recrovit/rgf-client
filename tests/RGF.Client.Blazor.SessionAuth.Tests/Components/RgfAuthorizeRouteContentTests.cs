using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authorization.RouteAccess;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Components;

public sealed class RgfAuthorizeRouteContentTests
{
    [Fact]
    public void Render_ProtectedRoute_SetsRouteRequirementAndBeginsAuthenticationScope()
    {
        using var testContext = new Bunit.BunitContext();
        ConfigureAuthorizationServices(testContext.Services);
        var sessionMonitor = new RecordingSessionMonitor();
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(sessionMonitor);

        _ = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(ProtectedPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        Assert.Equal([true], sessionMonitor.RouteRequirements);
        Assert.Equal(1, sessionMonitor.BeginScopeCount);
        Assert.Equal(0, sessionMonitor.DisposeCount);
    }

    [Fact]
    public void Render_PublicRoute_ClearsRouteRequirementWithoutAuthenticationScope()
    {
        using var testContext = new Bunit.BunitContext();
        ConfigureAuthorizationServices(testContext.Services);
        var sessionMonitor = new RecordingSessionMonitor();
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(sessionMonitor);

        _ = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(PublicPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        Assert.Equal([false], sessionMonitor.RouteRequirements);
        Assert.Equal(0, sessionMonitor.BeginScopeCount);
        Assert.Equal(0, sessionMonitor.DisposeCount);
    }

    [Fact]
    public void Render_SwitchingFromProtectedToPublicRoute_ClearsRequirementAndDisposesScope()
    {
        using var testContext = new Bunit.BunitContext();
        ConfigureAuthorizationServices(testContext.Services);
        var sessionMonitor = new RecordingSessionMonitor();
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(sessionMonitor);

        var cut = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(ProtectedPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        cut.Dispose();

        _ = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(PublicPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        Assert.Equal([true, false], sessionMonitor.RouteRequirements);
        Assert.Equal(1, sessionMonitor.BeginScopeCount);
        Assert.True(sessionMonitor.DisposeCount is 0 or 1);
    }

    [Fact]
    public async Task Render_ProtectedRouteWithInvalidSession_RedirectsToLogin()
    {
        using var testContext = new Bunit.BunitContext();
        ConfigureAuthorizationServices(testContext.Services);
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(serviceProvider =>
            CreateSessionMonitor(serviceProvider.GetRequiredService<NavigationManager>(), sessionExpired: true));
        var sessionMonitor = testContext.Services.GetRequiredService<IRgfAuthenticationSessionMonitor>();

        await sessionMonitor.HandleUnauthorizedAsync(new RgfAuthenticationFailureContext
        {
            IsReauthenticationRequired = true,
            RequestUri = "/rgf/api/entity/RecroGrid",
            StatusCode = System.Net.HttpStatusCode.Unauthorized,
            ResponseHeaders = new Dictionary<string, string[]>()
        }, CancellationToken.None);

        _ = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(ProtectedPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        var navigationManager = (BunitNavigationManager)testContext.Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/authentication/login?returnUrl=%2F", navigationManager.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_PublicRouteWithInvalidSession_DoesNotRedirect()
    {
        using var testContext = new Bunit.BunitContext();
        ConfigureAuthorizationServices(testContext.Services);
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(serviceProvider =>
            CreateSessionMonitor(serviceProvider.GetRequiredService<NavigationManager>()));
        var sessionMonitor = testContext.Services.GetRequiredService<IRgfAuthenticationSessionMonitor>();

        await sessionMonitor.HandleUnauthorizedAsync(new RgfAuthenticationFailureContext
        {
            IsReauthenticationRequired = true,
            RequestUri = "/rgf/api/entity/RecroGrid",
            StatusCode = System.Net.HttpStatusCode.Unauthorized,
            ResponseHeaders = new Dictionary<string, string[]>()
        }, CancellationToken.None);

        _ = testContext.Render<RgfAuthorizeRouteContent>(parameters => parameters
            .Add(component => component.RouteData, CreateRouteData(typeof(PublicPage)))
            .Add(component => component.DefaultContent, CreateDefaultContent()));

        var navigationManager = (BunitNavigationManager)testContext.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("http://localhost/", navigationManager.Uri);
    }

    private static IRgfAuthenticationSessionMonitor CreateSessionMonitor(NavigationManager navigationManager, bool sessionExpired = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();
        var sessionMonitorType = typeof(IRgfAuthenticationSessionMonitor).Assembly.GetType(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session.RgfAuthenticationSessionMonitor",
            throwOnError: true)!;

        return (IRgfAuthenticationSessionMonitor)ActivatorUtilities.CreateInstance(
            serviceProvider,
            sessionMonitorType,
            new StubHttpClientFactory(sessionExpired),
            navigationManager,
            new RgfAuthenticationEndpointResolver(configuration));
    }

    private static RouteData CreateRouteData(Type pageType) => new(pageType, new Dictionary<string, object?>());

    private static RenderFragment CreateDefaultContent() => builder => builder.AddMarkupContent(0, "<h1>Protected content</h1>");

    private static void ConfigureAuthorizationServices(IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationService, SimpleAuthorizationService>();
        services.AddSingleton<AuthenticationStateProvider, AuthenticatedTestAuthenticationStateProvider>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build());
        services.AddSingleton<RgfAuthenticationEndpointResolver>();
    }

    [Authorize]
    private sealed class ProtectedPage : ComponentBase;

    [AllowAnonymous]
    private sealed class PublicPage : ComponentBase;

    private sealed class RecordingSessionMonitor : IRgfAuthenticationSessionMonitor
    {
        public bool HasValidSession => true;

        public List<bool> RouteRequirements { get; } = [];

        public int BeginScopeCount { get; private set; }

        public int DisposeCount { get; private set; }

        public event Action? SessionStateChanged
        {
            add { }
            remove { }
        }

        public IDisposable BeginAuthenticationRequirementScope()
        {
            BeginScopeCount++;
            return new Scope(this);
        }

        public ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public Task EnsureValidatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ProbeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void SetRouteAuthenticationRequired(bool requiresAuthentication)
            => RouteRequirements.Add(requiresAuthentication);

        private sealed class Scope(RecordingSessionMonitor owner) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    owner.DisposeCount++;
                }
            }
        }
    }

    private sealed class StubHttpClientFactory(bool sessionExpired) : IHttpClientFactory
    {
        private readonly bool _sessionExpired = sessionExpired;

        public HttpClient CreateClient(string name) => new(new StubHandler(_sessionExpired))
        {
            BaseAddress = new Uri("http://localhost")
        };

        private sealed class StubHandler(bool sessionExpired) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (sessionExpired)
                {
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                    response.Headers.Add(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderName, RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderValue);
                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
            }
        }
    }
}
