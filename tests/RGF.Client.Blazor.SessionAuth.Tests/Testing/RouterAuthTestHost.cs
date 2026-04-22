using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Recrovit.AspNetCore.Components.Routing;
using Recrovit.AspNetCore.Components.Routing.Attributes;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Tests.Testing;

internal sealed class RouterAuthTestHost : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingAuthenticationState>(0);
        builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(childBuilder =>
        {
            childBuilder.OpenComponent<RecrovitRoutes>(0);
            childBuilder.AddAttribute(1, nameof(RecrovitRoutes.Kind), RecrovitRoutesKind.Client);
            childBuilder.AddAttribute(2, nameof(RecrovitRoutes.AppAssembly), typeof(PublicInteractiveAutoPage).Assembly);
            childBuilder.AddAttribute(3, nameof(RecrovitRoutes.DefaultLayout), typeof(RouterAuthTestLayout));
            childBuilder.AddAttribute(4, nameof(RecrovitRoutes.NotFoundPage), typeof(RouterAuthNotFoundPage));
            childBuilder.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

public sealed class RouterAuthTestLayout : LayoutComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "id", "router-auth-layout");
        builder.AddContent(2, Body);
        builder.CloseElement();
    }
}

public abstract class RouterAuthPageBase : ComponentBase
{
    [Parameter]
    public string Marker { get; set; } = string.Empty;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "id", "page-marker");
        builder.AddContent(2, Marker);
        builder.CloseElement();
    }
}

[Route("/public")]
[RecrovitPageRoute(RecrovitRouteMode.InteractiveAuto)]
public sealed class PublicInteractiveAutoPage : RouterAuthPageBase
{
    public PublicInteractiveAutoPage()
    {
        Marker = "public-page";
    }
}

[Route("/public-next")]
[RecrovitPageRoute(RecrovitRouteMode.InteractiveAuto)]
public sealed class NextPublicInteractiveAutoPage : RouterAuthPageBase
{
    public NextPublicInteractiveAutoPage()
    {
        Marker = "public-next-page";
    }
}

[Route("/protected")]
[Authorize]
[RecrovitPageRoute(RecrovitRouteMode.InteractiveAuto)]
public sealed class ProtectedInteractiveAutoPage : RouterAuthPageBase
{
    public ProtectedInteractiveAutoPage()
    {
        Marker = "protected-page";
    }
}

[Route("/protected-next")]
[Authorize]
[RecrovitPageRoute(RecrovitRouteMode.InteractiveAuto)]
public sealed class NextProtectedInteractiveAutoPage : RouterAuthPageBase
{
    public NextProtectedInteractiveAutoPage()
    {
        Marker = "protected-next-page";
    }
}

[Route("/protected-init")]
[Authorize]
[RecrovitPageRoute(RecrovitRouteMode.InteractiveAuto)]
public sealed class ProtectedInitializationTrackingPage : ComponentBase
{
    public static int InitializationCount { get; private set; }

    public static void Reset() => InitializationCount = 0;

    protected override Task OnInitializedAsync()
    {
        InitializationCount++;
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "id", "page-marker");
        builder.AddContent(2, "protected-init-page");
        builder.CloseElement();
    }
}

[Route("/router-auth-not-found")]
public sealed class RouterAuthNotFoundPage : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "id", "not-found");
        builder.AddContent(2, "router-auth-not-found");
        builder.CloseElement();
    }
}

internal sealed class AuthenticatedTestAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AuthenticatedState = new(
        new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Demo User")],
                authenticationType: "TestAuth")));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(AuthenticatedState);
}

internal sealed class AnonymousTestAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(AnonymousState);
}

internal sealed class TransitioningAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly object _sync = new();
    private AuthenticationState _state;

    public TransitioningAuthenticationStateProvider(bool isAuthenticated = false)
    {
        _state = CreateState(isAuthenticated);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        lock (_sync)
        {
            return Task.FromResult(_state);
        }
    }

    public void SetAuthenticated(bool isAuthenticated, bool notify = true)
    {
        Task<AuthenticationState>? stateTask = null;

        lock (_sync)
        {
            _state = CreateState(isAuthenticated);
            if (notify)
            {
                stateTask = Task.FromResult(_state);
            }
        }

        if (stateTask is not null)
        {
            NotifyAuthenticationStateChanged(stateTask);
        }
    }

    public void RaiseAuthenticationStateChanged()
    {
        Task<AuthenticationState> stateTask;

        lock (_sync)
        {
            stateTask = Task.FromResult(_state);
        }

        NotifyAuthenticationStateChanged(stateTask);
    }

    private static AuthenticationState CreateState(bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        return new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Hydrated Demo User")],
                authenticationType: "TestAuth")));
    }
}

internal sealed class SessionAwareAuthenticatedTestAuthenticationStateProvider(IRgfAuthenticationSessionMonitor sessionMonitor) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AuthenticatedState = new(
        new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Demo User")],
                authenticationType: "TestAuth")));

    private static readonly AuthenticationState AnonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(sessionMonitor.HasValidSession ? AuthenticatedState : AnonymousState);
}

internal sealed class SimpleAuthorizationService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            switch (requirement)
            {
                case DenyAnonymousAuthorizationRequirement:
                    if (user.Identity?.IsAuthenticated is not true)
                    {
                        return Task.FromResult(AuthorizationResult.Failed());
                    }
                    break;
                case RolesAuthorizationRequirement rolesRequirement:
                    if (rolesRequirement.AllowedRoles.All(role => !user.IsInRole(role)))
                    {
                        return Task.FromResult(AuthorizationResult.Failed());
                    }
                    break;
            }
        }

        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        => throw new NotSupportedException("Named authorization policies are not used in these tests.");
}

internal sealed class RecordingRedirectingSessionMonitor(NavigationManager navigationManager) : IRgfAuthenticationSessionMonitor
{
    private bool _hasValidSession = true;
    private int _loginNavigationStarted;

    public bool HasValidSession => _hasValidSession;

    public List<bool> RouteRequirements { get; } = [];

    public event Action? SessionStateChanged;

    public Task EnsureValidatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ProbeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void SetRouteAuthenticationRequired(bool requiresAuthentication)
    {
        RouteRequirements.Add(requiresAuthentication);
        if (requiresAuthentication && !_hasValidSession)
        {
            NavigateToLoginOnce();
        }
    }

    public IDisposable BeginAuthenticationRequirementScope()
    {
        if (!_hasValidSession)
        {
            NavigateToLoginOnce();
        }

        return NoOpScope.Instance;
    }

    public ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken)
    {
        if (context.IsReauthenticationRequired)
        {
            _hasValidSession = false;
            SessionStateChanged?.Invoke();
        }

        return ValueTask.CompletedTask;
    }

    public void InvalidateSession()
    {
        _hasValidSession = false;
        SessionStateChanged?.Invoke();
    }

    private void NavigateToLoginOnce()
    {
        if (Interlocked.Exchange(ref _loginNavigationStarted, 1) != 0)
        {
            return;
        }

        var currentUri = new Uri(navigationManager.Uri, UriKind.Absolute);
        var returnUrl = $"{currentUri.PathAndQuery}{currentUri.Fragment}";
        navigationManager.NavigateTo($"/authentication/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }

    private sealed class NoOpScope : IDisposable
    {
        public static NoOpScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class StubAuthenticationEndpointsHttpClientFactory : IHttpClientFactory
{
    private readonly StubAuthenticationEndpointsHandler _handler;

    public StubAuthenticationEndpointsHttpClientFactory(bool sessionExpired = true)
    {
        _handler = new StubAuthenticationEndpointsHandler(sessionExpired);
    }

    public int SessionRequestCount => _handler.SessionRequestCount;

    public int PrincipalRequestCount => _handler.PrincipalRequestCount;

    public bool SessionExpired
    {
        get => _handler.SessionExpired;
        set => _handler.SessionExpired = value;
    }

    public TimeSpan SessionResponseDelay
    {
        get => _handler.SessionResponseDelay;
        set => _handler.SessionResponseDelay = value;
    }

    public HttpClient CreateClient(string name)
        => new(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private sealed class StubAuthenticationEndpointsHandler(bool sessionExpired) : HttpMessageHandler
    {
        public bool SessionExpired { get; set; } = sessionExpired;

        public TimeSpan SessionResponseDelay { get; set; }

        public int SessionRequestCount { get; private set; }

        public int PrincipalRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendCoreAsync(request, cancellationToken);

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath;

            if (string.Equals(path, "/authentication/session", StringComparison.Ordinal))
            {
                SessionRequestCount++;
                if (SessionResponseDelay > TimeSpan.Zero)
                {
                    await Task.Delay(SessionResponseDelay, cancellationToken);
                }

                return CreateSessionResponse();
            }

            if (string.Equals(path, "/authentication/principal", StringComparison.Ordinal))
            {
                PrincipalRequestCount++;
                return CreatePrincipalResponse();
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage CreateSessionResponse()
        {
            if (!SessionExpired)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return CreateReauthenticationRequiredResponse();
        }

        private HttpResponseMessage CreatePrincipalResponse()
        {
            if (!SessionExpired)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        isAuthenticated = true,
                        authenticationType = "TestAuth",
                        nameClaimType = ClaimTypes.Name,
                        roleClaimType = ClaimTypes.Role,
                        claims = new[]
                        {
                            new { type = ClaimTypes.Name, value = "Demo User", valueType = ClaimValueTypes.String, issuer = "LOCAL AUTHORITY", originalIssuer = "LOCAL AUTHORITY" }
                        }
                    })
                };
            }

            return CreateReauthenticationRequiredResponse();
        }

        private static HttpResponseMessage CreateReauthenticationRequiredResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.Add(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderName, RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderValue);
            return response;
        }
    }
}
