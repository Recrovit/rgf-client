# RecroGrid Framework Client Packages

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Abstraction.svg?label=Recrovit.RecroGridFramework.Abstraction)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.svg?label=Recrovit.RecroGridFramework.Client)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.svg?label=Recrovit.RecroGridFramework.Client.Blazor)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.svg?label=Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.svg?label=Recrovit.RecroGridFramework.Client.Blazor.SessionAuth)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Blazor.RgfApexCharts.svg?label=Recrovit.RecroGridFramework.Blazor.ApexCharts)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.UI.svg?label=Recrovit.RecroGridFramework.Client.Blazor.UI)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

This repository contains the client-side packages of the RecroGrid Framework stack.

At a high level, these packages build on each other in layers:

1. [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) defines the shared contracts, models, and service abstractions used by both client-side packages and the server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) APIs.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) provides the core client-side service and orchestration layer that talks to the `/rgf/api/...` endpoints and exposes client runtime services such as API access, security, localization, menus, notifications, and handlers.
3. [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) adapts that client runtime to the Blazor component model with Blazor components, templates, configuration helpers, authentication integration, and JS/resource loading.
4. [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/) adds session-based authentication support for Blazor clients that authenticate through host-backed cookie/session endpoints instead of holding downstream bearer tokens in the browser.
5. [`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/) provides the matching ASP.NET Core host-side OpenID Connect, proxy, and SessionAuth SSR integration for Blazor Web App style applications.
6. [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) provides an ApexCharts-based chart implementation for the Blazor integration layer.
7. [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) provides the ready-to-use default Blazor UI layer with concrete menu, dialog, grid, form, filter, toolbar, toast, tree, and chart components.

## Package Summary

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/)
  Shared request/response contracts, models, constants, service interfaces, and infrastructure primitives. This is the common language between the RecroGrid client packages and the server-side Core APIs.
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
  Core client-side runtime layer. Implements API communication and provides DI registration, security, dictionaries, menus, progress, notifications, and higher-level handlers.
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
  Base Blazor integration layer. Exposes the RecroGrid client runtime through Blazor components, templates, auth helpers, dynamic component registration, and JS/resource initialization.
- [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/)
  Session-based authentication layer for RGF Blazor apps. Adds route-aware session validation, principal synchronization, SSR cookie forwarding, and host/downstream proxy invocation helpers for host-backed auth flows.
- [`Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect/)
  High-level ASP.NET Core host integration for Blazor Web Apps that use OpenID Connect sign-in, host-side proxying, and SessionAuth-based client authentication.
- [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)
  ApexCharts-based chart provider for the Blazor layer. Registers a concrete chart component and transforms RecroGrid aggregation data into ApexCharts series and options.
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/)
  Packaged default UI layer for Blazor applications. Supplies the out-of-the-box Bootstrap-based RecroGrid UI and wires in the default menu, dialog, entity, grid, form, pager, tree, toast, toolbar, and chart implementations.

## Repository Structure

- [src/RGF.Abstraction/README.md](src/RGF.Abstraction/README.md)
- [src/RGF.Client/README.md](src/RGF.Client/README.md)
- [src/RGF.Client.Blazor/README.md](src/RGF.Client.Blazor/README.md)
- [src/RGF.Client.Blazor.SessionAuth/README.md](src/RGF.Client.Blazor.SessionAuth/README.md)
- [src/RGF.Client.Blazor.Host.OpenIdConnect/README.md](src/RGF.Client.Blazor.Host.OpenIdConnect/README.md)
- [src/RGF.Blazor.RgfApexCharts/README.md](src/RGF.Blazor.RgfApexCharts/README.md)
- [src/RGF.Client.Blazor.UI/README.md](src/RGF.Client.Blazor.UI/README.md)

## Related Server Packages

- [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/): the server-side RecroGrid Framework Core implementation and the endpoints consumed by the client packages in this repository
- [`Recrovit.AspNetCore.Authentication.OpenIdConnect`](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/): reusable ASP.NET Core OpenID Connect host and downstream token/proxy infrastructure used by `RGF.Client.Blazor.Host.OpenIdConnect`
- [`Recrovit.AspNetCore.Components.RoutingCore`](https://www.nuget.org/packages/Recrovit.AspNetCore.Components.RoutingCore/): route classification and host/client rendering infrastructure used by the RGF Blazor host integration packages

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Core.svg?label=Recrovit.RecroGridFramework.Core)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrogrid.svg?label=Recrogrid)](https://www.nuget.org/packages/Recrogrid/) ![NuGet Downloads](https://img.shields.io/nuget/dt/RecroGrid)

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.AspNetCore.Authentication.OpenIdConnect?label=Recrovit.AspNetCore.Authentication.OpenIdConnect)](https://www.nuget.org/packages/Recrovit.AspNetCore.Authentication.OpenIdConnect/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.AspNetCore.Components.Routing.svg?label=Recrovit.AspNetCore.Components.RoutingCore)](https://www.nuget.org/packages/Recrovit.AspNetCore.Components.RoutingCore/)
