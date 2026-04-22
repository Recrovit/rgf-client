using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Components;

public sealed class RgfAuthorizeRouteRestoredClientReproTests
{
    [Fact]
    public void PublicInteractiveRoute_WithHydratingAuthenticatedClientAndExpiredServerSession_NavigatingToProtectedRoute_ShouldRedirectToLogin()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out var authenticationStateProvider, initiallyAuthenticated: false);
        authenticationHttpClientFactory.SessionResponseDelay = TimeSpan.FromMilliseconds(200);

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));
        AssertProductionSessionAwareAuthenticationStateProvider(testContext);

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");
        Assert.Empty(cut.FindAll("#page-marker"));

        authenticationStateProvider.SetAuthenticated(true);

        cut.WaitForAssertion(() =>
        {
            Assert.True(
                authenticationHttpClientFactory.SessionRequestCount == 1
                && navigationManager.Uri.EndsWith("/authentication/login?returnUrl=%2Fprotected", StringComparison.Ordinal)
                && cut.FindAll("#page-marker").Count == 0,
                $"Expected protected SPA navigation to revalidate the expired server session and redirect to login, but session probes={authenticationHttpClientFactory.SessionRequestCount}, uri='{navigationManager.Uri}', markerCount='{cut.FindAll("#page-marker").Count}'.");
        });
    }

    [Fact]
    public async Task PublicInteractiveRoute_AfterExplicitSessionProbe_NavigatingToProtectedRoute_RedirectsToLogin()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var sessionMonitor = testContext.Services.GetRequiredService<IRgfAuthenticationSessionMonitor>();
        await sessionMonitor.ProbeAsync(CancellationToken.None);

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.True(authenticationHttpClientFactory.SessionRequestCount > 0);
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PublicInteractiveRoute_NavigatingToProtectedRouteWithValidSession_RendersProtectedContent()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);
        authenticationHttpClientFactory.SessionExpired = false;

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });
    }

    [Fact]
    public void ValidatedSession_PublicProtectedPublicProtected_NavigatesWithSingleSessionProbe()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);
        authenticationHttpClientFactory.SessionExpired = false;

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });

        navigationManager.NavigateTo("/public-next");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/public-next", navigationManager.Uri);
            Assert.Equal("public-next-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });

        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });
    }

    [Fact]
    public void ValidatedSession_ProtectedToProtectedNavigation_DoesNotProbeAgain()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);
        authenticationHttpClientFactory.SessionExpired = false;

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });

        navigationManager.NavigateTo("/protected-next");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected-next", navigationManager.Uri);
            Assert.Equal("protected-next-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });
    }

    [Fact]
    public async Task UnauthorizedAfterSuccessfulValidation_ClearsValidationCache_AndNextProtectedNavigationReprobes()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);
        authenticationHttpClientFactory.SessionExpired = false;

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });

        authenticationHttpClientFactory.SessionExpired = true;
        var sessionMonitor = testContext.Services.GetRequiredService<IRgfAuthenticationSessionMonitor>();
        await sessionMonitor.HandleUnauthorizedAsync(CreateUnauthorizedContext(), CancellationToken.None);

        navigationManager.NavigateTo("/public-next");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/public-next", navigationManager.Uri);
            Assert.Equal("public-next-page", cut.Find("#page-marker").TextContent);
        });

        var sessionRequestCountAfterPublicNavigation = authenticationHttpClientFactory.SessionRequestCount;

        navigationManager.NavigateTo("/protected");

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
            Assert.True(authenticationHttpClientFactory.SessionRequestCount > sessionRequestCountAfterPublicNavigation);
        });
    }

    [Fact]
    public void PublicInteractiveRoute_NavigatingToAnotherPublicRoute_DoesNotProbeSession()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _);

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/public-next");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/public-next", navigationManager.Uri);
            Assert.Equal("public-next-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(0, authenticationHttpClientFactory.SessionRequestCount);
        });
    }

    [Fact]
    public void ProtectedRoute_DoesNotRenderContentBeforeSessionProbeCompletes()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out _, initiallyAuthenticated: false);
        authenticationHttpClientFactory.SessionResponseDelay = TimeSpan.FromMilliseconds(200);

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");

        Assert.Empty(cut.FindAll("#page-marker"));
        Assert.Equal("http://localhost/protected", navigationManager.Uri);

        cut.WaitForAssertion(() =>
        {
            Assert.EndsWith("/authentication/login?returnUrl=%2Fprotected", navigationManager.Uri, StringComparison.Ordinal);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
        });
    }

    [Fact]
    public void PublicInteractiveRoute_WithHydratingAuthenticatedClientAndValidServerSession_NavigatingToProtectedRoute_RendersProtectedContent()
    {
        using var testContext = CreateTestContext(out var authenticationHttpClientFactory, out var authenticationStateProvider, initiallyAuthenticated: false);
        authenticationHttpClientFactory.SessionExpired = false;
        authenticationHttpClientFactory.SessionResponseDelay = TimeSpan.FromMilliseconds(200);

        NavigateTo(testContext, "http://localhost/public");
        var cut = testContext.Render<RouterAuthTestHost>();

        cut.WaitForAssertion(() => Assert.Equal("public-page", cut.Find("#page-marker").TextContent));
        AssertProductionSessionAwareAuthenticationStateProvider(testContext);

        var navigationManager = GetNavigationManager(testContext);
        navigationManager.NavigateTo("/protected");
        Assert.Empty(cut.FindAll("#page-marker"));

        authenticationStateProvider.SetAuthenticated(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("http://localhost/protected", navigationManager.Uri);
            Assert.Equal("protected-page", cut.Find("#page-marker").TextContent);
            Assert.Equal(1, authenticationHttpClientFactory.SessionRequestCount);
            Assert.True(authenticationHttpClientFactory.PrincipalRequestCount > 0);
        });
    }

    private static BunitContext CreateTestContext(
        out StubAuthenticationEndpointsHttpClientFactory authenticationHttpClientFactory,
        out TransitioningAuthenticationStateProvider authenticationStateProvider,
        bool initiallyAuthenticated = true)
    {
        var testContext = new BunitContext();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        authenticationHttpClientFactory = new StubAuthenticationEndpointsHttpClientFactory(sessionExpired: true);
        authenticationStateProvider = new TransitioningAuthenticationStateProvider(initiallyAuthenticated);

        testContext.Services.AddLogging();
        testContext.Services.AddRgfBlazorSessionAuthClientServices(configuration, apiBaseAddressOverride: "http://localhost");
        testContext.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationService, SimpleAuthorizationService>();
        testContext.Services.AddSingleton<IConfiguration>(configuration);
        testContext.Services.AddSingleton<IHttpClientFactory>(authenticationHttpClientFactory);
        testContext.Services.AddSingleton(authenticationStateProvider);
        testContext.Services.AddSingleton<IRgfAuthenticationSessionMonitor>(serviceProvider =>
            CreateProductionSessionMonitor(
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<NavigationManager>(),
                serviceProvider.GetRequiredService<RgfAuthenticationEndpointResolver>()));
        testContext.Services.AddSingleton<AuthenticationStateProvider>(serviceProvider =>
            CreateProductionSessionAwareAuthenticationStateProvider(
                serviceProvider,
                serviceProvider.GetRequiredService<TransitioningAuthenticationStateProvider>(),
                serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>()));

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

    private static void AssertProductionSessionAwareAuthenticationStateProvider(BunitContext testContext)
        => Assert.Equal(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.StateProvider.RgfSessionAwareAuthenticationStateProvider",
            testContext.Services.GetRequiredService<AuthenticationStateProvider>().GetType().FullName);

    private static void NavigateTo(BunitContext testContext, string uri)
        => testContext.Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    private static RgfAuthenticationFailureContext CreateUnauthorizedContext()
        => new()
        {
            IsReauthenticationRequired = true,
            RequestUri = "/rgf/api/entity/RecroGrid",
            StatusCode = System.Net.HttpStatusCode.Unauthorized,
            ResponseHeaders = new Dictionary<string, string[]>()
        };

    private static IRgfAuthenticationSessionMonitor CreateProductionSessionMonitor(
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        RgfAuthenticationEndpointResolver authenticationEndpointResolver)
    {
        var sessionMonitorType = typeof(IRgfAuthenticationSessionMonitor).Assembly.GetType(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session.RgfAuthenticationSessionMonitor",
            throwOnError: true)!;

        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();

        return (IRgfAuthenticationSessionMonitor)ActivatorUtilities.CreateInstance(
            serviceProvider,
            sessionMonitorType,
            httpClientFactory,
            navigationManager,
            authenticationEndpointResolver);
    }

    private static AuthenticationStateProvider CreateProductionSessionAwareAuthenticationStateProvider(
        IServiceProvider serviceProvider,
        AuthenticationStateProvider innerProvider,
        IRgfAuthenticationSessionMonitor sessionMonitor)
    {
        var assembly = typeof(IRgfAuthenticationSessionMonitor).Assembly;
        var synchronizerType = assembly.GetType(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.PrincipalSync.RgfAuthenticationPrincipalSnapshotSynchronizer",
            throwOnError: true)!;
        var sessionAwareProviderType = assembly.GetType(
            "Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.StateProvider.RgfSessionAwareAuthenticationStateProvider",
            throwOnError: true)!;
        var synchronizer = ServiceProviderServiceExtensions.GetRequiredService(serviceProvider, synchronizerType);

        return (AuthenticationStateProvider)ActivatorUtilities.CreateInstance(
            serviceProvider,
            sessionAwareProviderType,
            innerProvider,
            sessionMonitor,
            synchronizer);
    }
}
