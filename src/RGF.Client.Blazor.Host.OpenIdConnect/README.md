# RecroGrid.Framework.Client.Blazor.Host.OpenIdConnect

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.svg?label=Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/) [![NuGet Version](https://img.shields.io/nuget/v/Recrovit.AspNetCore.Authentication.OpenIdConnect.svg?label=Recrovit.AspNetCore.Authentication.OpenIdConnect)](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/) [![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.svg?label=Recrovit.RecroGridFramework.Client.Blazor.SessionAuth)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/) is the high-level ASP.NET Core host integration package for Blazor Web App style applications that use:

- OpenID Connect sign-in at the host
- a cookie-backed authenticated session on the host
- downstream API access through host-side proxy endpoints instead of direct browser bearer tokens
- SessionAuth on the interactive Blazor client

RGF stands for [RecroGrid Framework](https://RecroGridFramework.com).

Its purpose is to package the "host half" of the SessionAuth server-proxy architecture into one reusable setup layer.

Although this package is part of the RGF ecosystem, it is not limited to RGF-only scenarios. It builds on the reusable OpenID Connect host and token-management infrastructure from [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/), so the same host-side sign-in, session, downstream token acquisition, and proxy pattern can also be used for non-RGF APIs.

In practice, this package combines:

- the reusable OpenID Connect host infrastructure from [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/)
- the SSR-side registration from [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)
- opinionated proxy and host wiring for the typical RGF application shape
- the Razor Components setup typically needed by an interactive Blazor Web App host

Because of this, the package is the recommended app-level entry point when the browser should stay on the host origin, and the ASP.NET Core host should handle authentication, acquire downstream access tokens, and proxy the API calls on the user's behalf.

The downstream API can be an RGF API or any other API registered under `Recrovit:OpenIdConnect:DownstreamApis`, regardless of whether that API is hosted together with the app or on a separate server.

For SSR-originated RGF calls, the package also uses host-aware cookie forwarding so server-side requests can participate in the same authenticated host session as the browser.

## How It Fits Into The SessionAuth Architecture

At a high level, the flow looks like this:

1. The interactive Blazor client is registered with [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/) in `ServerProxy` mode.
2. Browser requests target the local host origin instead of calling the downstream RGF API directly.
3. The host authenticates the user with OpenID Connect and keeps the authenticated session in a secure cookie.
4. This package maps the `/authentication/...` endpoints and the host-side proxy infrastructure on the host.
5. When the host proxies an authenticated request, it obtains or refreshes the downstream access token for the configured API entry and forwards the request to the real downstream API.
6. SessionAuth on the client uses the host authentication endpoints to validate the session, inspect the principal snapshot, and redirect to login when reauthentication is required.

This makes the package the bridge between:

- the client-side SessionAuth experience
- the host-side authenticated session
- the downstream API registration

## Typical Use Case

Use this package when all of the following are true:

- your app is a Blazor Web App or hybrid host with a server-side ASP.NET Core application
- the browser should not hold or manage downstream bearer tokens directly
- the host should own login, logout, session validation, and principal snapshot endpoints
- the host should proxy downstream API traffic on the user's behalf
- the interactive client is registered with `AddRgfBlazorSessionAuthClientServices(...)`

If the browser calls the API directly with bearer tokens, use a direct client-side auth setup (`AddRgfBlazorWasmBearerServices`) instead of this package.

## Usage Guide

### 1. Register the server-side host package

In the server-side app `Program.cs`, register the host package and map its endpoints/components. The Recrovit component routing setup stays explicit, while the host package wrappers add the Razor Components, OIDC host, SessionAuth SSR, and component-mapping infrastructure automatically.

```csharp
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Configuration;
using Recrovit.RecroGridFramework.Client.Blazor.UI;

var builder = WebApplication.CreateBuilder(args);

// Required so the host can discover the server and client routes used by the shared Blazor app.
builder.Services.AddRecrovitComponentRouting(options =>
{
    options.AddRouteAssembly(typeof(App).Assembly);
    options.AddRouteAssembly(typeof(BlazorApp.Client._Imports).Assembly);
    options.DefaultLayout = typeof(MainLayout);
    options.SetNotFoundPage(RecrovitRoutesKind.Host, typeof(BlazorApp.Client.Pages.NotFound));
});

// Required to register the OIDC-aware host services together with the standard ASP.NET Core/Blazor infrastructure they wrap.
builder.AddRgfBlazorServerProxyOpenIdConnectRazorComponents();
builder.AddRgfBlazorServerProxyOpenIdConnectHost();

var app = builder.Build();

app.UseHttpsRedirection();

// Required to expose the proxy endpoints, initialize RGF services, and map the shared root component.
app.MapRgfBlazorServerProxyOpenIdConnectEndpoints("/not-found");

// Required to initialize the server-side RGF runtime before the app starts serving requests.
await app.Services.InitializeRgfBlazorServerAsync();
await app.Services.InitializeRgfUIAsync(loadResources: false);

// Required to map the shared app root so the host can render the client/server Razor components.
app.MapRgfBlazorServerProxyOpenIdConnectComponents<App>(typeof(BlazorApp.Client._Imports).Assembly);

await app.RunAsync();
```

These calls automatically add the following ASP.NET Core/Blazor infrastructure, so you do not need to register the standard framework services separately for this integration:

- `builder.AddRgfBlazorServerProxyOpenIdConnectRazorComponents()`
  Razor Components, interactive server render mode, interactive WebAssembly render mode, and authentication state serialization
- `builder.AddRgfBlazorServerProxyOpenIdConnectHost()`
  cascading authentication state, antiforgery, distributed memory cache, `IHttpContextAccessor`, the default and downstream proxy `HttpClient` registrations, ASP.NET Core Cookie/OpenID Connect authentication, ASP.NET Core authorization, and Data Protection setup
- `app.MapRgfBlazorServerProxyOpenIdConnectComponents<App>(...)`
  static assets mapping, root Razor component mapping, interactive server/WebAssembly render modes on the mapped root, and optional additional client assembly mapping

That means you should not separately repeat `AddRazorComponents()`, `AddAuthentication()`, `AddAuthorization()`, `AddAntiforgery()` and similar framework registrations unless you are intentionally customizing the underlying setup.

`MapRgfBlazorServerProxyOpenIdConnectEndpoints(...)` also applies the standard middleware sequence used by the underlying OIDC host package:

- forwarded headers
- status-code page re-execution
- authentication, authorization, and antiforgery middleware
- proxy transports plus `/authentication/...` and RGF proxy endpoint mapping

### 2. Configure the shared app root for route-aware host rendering

Update `App.razor` so the shared root component can choose the correct renderer for each route.

For additional routing configuration options, see [`Recrovit.AspNetCore.Components.Routing`](https://www.nuget.org/packages/Recrovit.AspNetCore.Components.Routing/).

This setup lets the host render server-routed pages through `RecrovitRoutes` while still handing WebAssembly-style routes off to the interactive client app. It is needed because the OIDC host package and the RGF proxy endpoints live on the server-side host, so server routes must stay on the host pipeline to get the expected authentication, reconnect, and proxy behavior.

```razor
<head>
    <HeadOutlet @rendermode="CurrentRenderMode" />
</head>

<body>
    @if (CurrentPageDefinition.RouteMode is RecrovitRouteMode.StaticServer or RecrovitRouteMode.InteractiveServer)
    {
        <RecrovitRoutes @rendermode="CurrentRenderMode" Kind="RecrovitRoutesKind.Host" />
        @if (CurrentPageDefinition.RouteMode == RecrovitRouteMode.InteractiveServer)
        {
            <ReconnectModal />
        }
    }
    else
    {
        <BlazorApp.Client.Routes @rendermode="CurrentRenderMode" />
    }
    <script src="@Assets["_framework/blazor.web.js"]"></script>
</body>

</html>

@inject RecrovitRouteModeResolver RecrovitRouteModeResolver
@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private RecrovitPageRouteDefinition CurrentPageDefinition
        => RecrovitRouteModeResolver.Resolve(HttpContext?.Request.Path.Value);

    private IComponentRenderMode? CurrentRenderMode
        => RecrovitRouteModeMapper.GetDefaultTopLevelRenderMode(CurrentPageDefinition.RouteMode);
}
```

### 3. Register the downstream API in the server-side app

The host package proxies to a named downstream API called `RgfApi`.

For additional OpenID Connect host and downstream API configuration options, see [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/).

In the server-side app configuration, register that downstream API under `Recrovit:OpenIdConnect:DownstreamApis:RgfApi` and point it to the real downstream API.

Minimal example:

```json
{
  "Recrovit": {
    "OpenIdConnect": {
      "Provider": "MainProvider",
      "Providers": {
        "MainProvider": {
          "Authority": "https://idp.example.com",
          "ClientId": "client-id",
          "ClientSecret": "client-secret",
          "Scopes": [ "openid", "profile", "offline_access" ]
          "CallbackPath": "/signin-oidc",
          "SignedOutRedirectPath": "/"
        }
      },
      "DownstreamApis": {
        "RgfApi": {
          "BaseUrl": "https://rgf-api-app.example.com",
          "Scopes": [ "api.scope" ]
        }
      }
    }
    "RecroGridFramework": {
      "API": {
        "ProxyBaseAddress": "https://app-host.example.com"
      }
    }
  }
}
```

Why this registration matters:

- the proxy routes in this package look up the downstream API by the name `RgfApi`
- the host uses that entry to resolve the downstream base URL
- the OIDC host infrastructure uses that entry to request and refresh access tokens for the downstream API

### 4. Configure the interactive client with SessionAuth

This package is only the server-side half of the solution.

For the client-side configuration details, see the [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth` README](../RGF.Client.Blazor.SessionAuth/README.md), especially its usage guide.

That client-side registration is responsible for:

- route-aware authorization wrapping
- session probing through `/authentication/session`
- principal snapshot synchronization through `/authentication/principal`
- redirecting protected client routes to `/authentication/login?returnUrl=...` when reauthentication is required

This host package supplies the matching server-side infrastructure that those client-side behaviors depend on.

## Built-In Authentication Endpoints Used By SessionAuth

Through the underlying OIDC host package, the host exposes these reusable endpoints under the configured base path, which defaults to `/authentication`:

- `GET /authentication/login`
- `POST /authentication/logout`
- `GET /authentication/session`
- `GET /authentication/principal`

SessionAuth depends specifically on:

- `login` for reauthentication redirects
- `session` for determining whether the current host session is still valid
- `principal` for rebuilding the interactive client-side `ClaimsPrincipal` from the host session

## Related Packages

- [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/): reusable OpenID Connect host infrastructure used by this package
- [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/): client-side and SSR session-auth support for RGF Blazor applications
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level Blazor UI package typically used by the interactive client
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): base Blazor integration layer for the RGF client stack
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): core RGF client runtime and HTTP services
