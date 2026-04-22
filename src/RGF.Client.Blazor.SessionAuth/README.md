# Recrovit.RecroGridFramework.Client.Blazor.SessionAuth

`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth` adds session-based authentication support to RGF Blazor applications that talk to the API through host-backed authentication endpoints.

Use it together with `Recrovit.RecroGridFramework.Client.Blazor.UI` when:

- the Blazor client does not call the API with bearer tokens directly
- authentication is handled by the host application through cookie-backed endpoints
- protected routes should automatically validate the current session and redirect to login when reauthentication is required

## What The Package Adds

The package extends the base Blazor client integration with session-auth specific behavior:

- route-aware authorization wrapping for Recrovit client routes
- session probing against the configured authentication session endpoint
- authenticated principal snapshot synchronization through the host authentication endpoints
- `AuthenticationStateProvider` decoration so an expired host session is reflected in Blazor auth state
- SSR cookie forwarding for RGF HTTP clients used from the host

## Registration API

The package exposes two registration entry points:

- `AddRgfBlazorSessionAuthClientServices(...)`: for the interactive Blazor client
- `AddRgfBlazorSessionAuthSsrServices(...)`: low-level SSR registration used by host-side integrations

`AddRgfBlazorSessionAuthClientServices(...)` configures:

- RGF client services in `ServerProxy` auth mode
- route wrapping with `RgfAuthorizeRouteContent`
- `AuthorizationCore`
- cascading authentication state support
- authentication state deserialization
- session monitoring and principal snapshot synchronization
- `AuthenticationStateProvider` decoration for session-aware auth state
- a Blazor initialization hook that probes the current session on client startup

`AddRgfBlazorSessionAuthSsrServices(...)` configures:

- RGF client services in `ServerProxySsr` auth mode
- SSR cookie forwarding for the RGF HTTP clients
- a no-op session monitor for the host-side rendering path

## Short Usage Guide

### 1. Add the package to the Blazor client

Reference at least:

- `Recrovit.RecroGridFramework.Client.Blazor.UI`
- `Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`

In the usual application setup, SessionAuth is added together with `Recrovit.RecroGridFramework.Client.Blazor.UI`, while the base `Recrovit.RecroGridFramework.Client.Blazor` package remains a transitive building block.

### 2. Register the client-side services

In the client `Program.cs`, register SessionAuth and the Recrovit component routing services:

```csharp
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth;
using Recrovit.RecroGridFramework.Client.Blazor.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddRgfBlazorSessionAuthClientServices(
    builder.Configuration,
    apiBaseAddressOverride: builder.HostEnvironment.BaseAddress);

builder.Services.AddRecrovitComponentRouting(options =>
{
    options.AddRouteAssembly(typeof(Program).Assembly);
});

var host = builder.Build();

await host.Services.InitializeRgfBlazorAsync();
await host.Services.InitializeRgfUIAsync();

await host.RunAsync();
```

For a Blazor Web App client, `apiBaseAddressOverride: builder.HostEnvironment.BaseAddress` makes the browser call the local host/proxy instead of the external API directly.

### 3. Configure `Routes.razor`

The client routes should be rendered through `RecrovitRoutes` so the SessionAuth route authorization wrapper can take effect:

```razor
@using Recrovit.AspNetCore.Components.Routing
@using Recrovit.AspNetCore.Components.Routing.Models

<RecrovitRoutes Kind="RecrovitRoutesKind.Client"
                AppAssembly="typeof(_Imports).Assembly"
                DefaultLayout="typeof(MainLayout)"
                NotFoundPage="typeof(Pages.NotFound)" />
```

### 4. Configure the server-side host

For the typical server-side setup, use [`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/) and configure the host according to that package's documentation.

That package is the recommended app-level entry point for the SessionAuth server-side scenario, including the OpenID Connect host setup, proxy configuration, and underlying SessionAuth SSR registration.

## Runtime Behavior

When the interactive client starts, the package probes the host session endpoint.

When the user navigates to a protected route:

- the route auth wrapper checks whether authentication is required
- the session monitor validates the current session
- the principal snapshot is synchronized when the user is authenticated
- if the host indicates that reauthentication is required, the client redirects to the configured login endpoint with a `returnUrl`

Public routes continue to render without login redirection even after session invalidation.

## Related Packages

- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level Blazor UI package that is typically registered together with SessionAuth
- [`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/): typical server-side host integration that brings in the SessionAuth SSR infrastructure
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): core RGF Blazor integration
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): base client runtime and HTTP services
