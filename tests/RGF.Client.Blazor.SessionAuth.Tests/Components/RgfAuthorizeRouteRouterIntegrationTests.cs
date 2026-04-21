using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Components;

public sealed class RgfAuthorizeRouteRouterIntegrationTests : BunitContext
{
    public RgfAuthorizeRouteRouterIntegrationTests()
    {
        Services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Services.AddRgfBlazorSessionAuthClientServices(configuration, apiBaseAddressOverride: "http://localhost");
        Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationService, SimpleAuthorizationService>();
        Services.AddSingleton<AuthenticationStateProvider, AuthenticatedTestAuthenticationStateProvider>();
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton<IRgfAuthenticationSessionMonitor>(serviceProvider =>
            new RecordingRedirectingSessionMonitor(serviceProvider.GetRequiredService<NavigationManager>()));
        Services.AddRecrovitComponentRouting(options =>
        {
            options.AddRouteAssembly(typeof(PublicInteractiveAutoPage).Assembly);
            options.DefaultLayout = typeof(RouterAuthTestLayout);
            options.SetNotFoundPage(RecrovitRoutesKind.Client, typeof(RouterAuthNotFoundPage));
        });
    }

    [Fact]
    public void PublicInteractiveRoute_AfterSessionInvalidation_NavigatingToProtectedRoute_ShouldRedirectToLogin()
    {
        NavigateTo("http://localhost/public");
        var cut = Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var sessionMonitor = GetSessionMonitor();
        sessionMonitor.InvalidateSession();

        var navigationManager = GetNavigationManager();
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
            Assert.Equal([false, true], sessionMonitor.RouteRequirements);
        });
    }

    [Fact]
    public void ProtectedInteractiveRoute_WhenSessionAlreadyInvalid_ShouldRedirectToLogin()
    {
        NavigateTo("http://localhost/protected");
        var sessionMonitor = GetSessionMonitor();
        sessionMonitor.InvalidateSession();

        var cut = Render<RouterAuthTestHost>();
        var navigationManager = GetNavigationManager();

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
            Assert.Equal([true], sessionMonitor.RouteRequirements);
        });
    }

    [Fact]
    public void PublicInteractiveRoute_AfterSessionInvalidation_NavigatingToAnotherPublicRoute_ShouldNotRedirectToLogin()
    {
        NavigateTo("http://localhost/public");
        var cut = Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var sessionMonitor = GetSessionMonitor();
        sessionMonitor.InvalidateSession();

        var navigationManager = GetNavigationManager();
        navigationManager.NavigateTo("/public-next");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/public-next", navigationManager.Uri);
            Assert.Equal("public-next-page", cut.Find("#page-marker").TextContent);
            Assert.Equal([false, false], sessionMonitor.RouteRequirements);
        });
    }

    private RecordingRedirectingSessionMonitor GetSessionMonitor()
        => (RecordingRedirectingSessionMonitor)Services.GetRequiredService<IRgfAuthenticationSessionMonitor>();

    private BunitNavigationManager GetNavigationManager()
        => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    private void NavigateTo(string uri)
        => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
}
