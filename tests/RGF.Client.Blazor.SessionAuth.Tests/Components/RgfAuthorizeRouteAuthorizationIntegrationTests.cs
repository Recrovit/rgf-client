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

public sealed class RgfAuthorizeRouteAuthorizationIntegrationTests
{
    [Fact]
    public void ProtectedRoute_WithAnonymousAuthenticationState_RedirectsToLoginWithoutRenderingPage()
    {
        using var testContext = CreateTestContext<AnonymousTestAuthenticationStateProvider>();

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("#page-marker"));
        });
    }

    [Fact]
    public void ProtectedRoute_WithAnonymousAuthenticationState_DoesNotRunPageInitialization()
    {
        using var testContext = CreateTestContext<AnonymousTestAuthenticationStateProvider>();
        ProtectedInitializationTrackingPage.Reset();

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected-init");

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected-init", navigationManager.Uri, StringComparison.Ordinal);
            Assert.Equal(0, ProtectedInitializationTrackingPage.InitializationCount);
        });
    }

    [Fact]
    public void PublicRoute_WithAnonymousAuthenticationState_RendersWithoutRedirect()
    {
        using var testContext = CreateTestContext<AnonymousTestAuthenticationStateProvider>();

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/public", GetNavigationManager(testContext).Uri);
            Assert.Equal("public-page", cut.Find("#page-marker").TextContent);
        });
    }

    private static BunitContext CreateTestContext<TAuthenticationStateProvider>()
        where TAuthenticationStateProvider : AuthenticationStateProvider
    {
        var testContext = new BunitContext();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        testContext.Services.AddLogging();
        testContext.Services.AddRgfBlazorSessionAuthClientServices(configuration, apiBaseAddressOverride: "http://localhost");
        testContext.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationService, SimpleAuthorizationService>();
        testContext.Services.AddSingleton<AuthenticationStateProvider, TAuthenticationStateProvider>();
        testContext.Services.AddSingleton<IConfiguration>(configuration);
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(serviceProvider =>
            new RecordingRedirectingSessionMonitor(serviceProvider.GetRequiredService<NavigationManager>()));
        testContext.Services.AddRecrovitComponentRouting(options =>
        {
            options.AddRouteAssembly(typeof(PublicInteractiveAutoPage).Assembly);
            options.DefaultLayout = typeof(RouterAuthTestLayout);
            options.SetNotFoundPage(RecrovitRoutesKind.Client, typeof(RouterAuthNotFoundPage));
        });

        return testContext;
    }

    private static BunitNavigationManager GetNavigationManager(BunitContext testContext)
        => (BunitNavigationManager)testContext.Services.GetRequiredService<NavigationManager>();

    private static void NavigateTo(BunitContext testContext, string uri)
        => testContext.Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
}
