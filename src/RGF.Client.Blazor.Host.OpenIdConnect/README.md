# RecroGrid Framework Client Blazor Host OpenIdConnect

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.svg?label=Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/) is the ASP.NET Core host integration package for the RecroGrid Framework Server-side Application model.

It integrates the Blazor host, OpenID Connect authentication, SessionAuth SSR services, authenticated host sessions, downstream API access, RGF proxy endpoints, static assets, and interactive server/WebAssembly render modes into one application-level setup.

The interactive `.Client` project uses [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/) and communicates through the host origin. The host manages the authenticated session and forwards RecroGrid Framework API requests to the configured downstream `RgfApi`.

## Documentation

The complete Server-side Application configuration is maintained in the RecroGrid Framework documentation:

- [Server-side Application configuration](https://recrovit.github.io/recrogrid-framework-docs/en/latest/docs/configuration/client/server-side-application.html)
- [Server-side Application Get Started](https://recrovit.github.io/recrogrid-framework-docs/en/latest/docs/get-started/server-side/)
- [Proxy-based API access](https://recrovit.github.io/recrogrid-framework-docs/en/latest/docs/configuration/api/proxy-based-access.html)
- [SessionAuth configuration](https://recrovit.github.io/recrogrid-framework-docs/en/latest/docs/configuration/authentication/session-auth.html)

The Server-side Application configuration covers:

- Host and interactive Client project setup
- Required NuGet packages
- Host and Client `Program.cs`
- Route-aware `App.razor` and `Routes.razor`
- OpenID Connect and `RgfApi` downstream API settings
- `ProxyBaseAddress`
- SessionAuth endpoint path synchronization
- Client configuration keys for localization, claims, security, and path-base hosting
- Runtime initialization and validation

## Proxy request protection

Authorized RGF HTTP routes and both progress hub routes apply the shared OIDC Origin/Fetch Metadata policy before access-token acquisition or downstream forwarding. Rejected requests return 403. RGF requests do not require antiforgery tokens; the generic OIDC downstream proxy still requires them for unsafe methods. Public resource, script, stylesheet, RecroDict and capability GET requests retain their existing behavior, and progress routes retain their existing authorization semantics.

Configure `Recrovit:OpenIdConnect:Host:DownstreamProxyRequestProtection` for HTTP and WebSocket origin whitelists. The Host's own origin is automatically allowed. `AllowSameSite` defaults to false; an HTTP whitelist does not override `Sec-Fetch-Site: cross-site`. WebSocket handshakes reject invalid, foreign and `null` origins; missing origins require the existing explicit opt-in.

SSR HTTP clients add the configured `API:ProxyBaseAddress` Host Origin only when targeting that same scheme and authority and both Origin and Sec-Fetch-Site are absent. Existing signals are preserved. The server-side progress client supplies the same Host Origin to its .NET SignalR transports; browser/WASM behavior is unchanged. Configure trusted forwarded headers before authentication and endpoint execution so the Host sees the public scheme and authority. The downstream `RgfApi` address is an independent trust boundary and may be on another server.

## Related Packages

- [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/)
- [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
