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

## Related Packages

- [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/)
- [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
