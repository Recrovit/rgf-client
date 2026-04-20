# RecroGrid Framework Client Packages

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Abstraction.svg?label=RGF.Abstraction)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.svg?label=RGF.Client)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.svg?label=RGF.Client.Blazor)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Blazor.RgfApexCharts.svg?label=RGF.Blazor.ApexCharts)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.UI.svg?label=RGF.Client.Blazor.UI)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/)
[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Core.svg?label=RGF.Core)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/)
![NuGet Downloads](https://img.shields.io/nuget/dt/RecroGrid)

This repository contains the client-side packages of the RecroGrid Framework stack.

At a high level, these packages build on each other in layers:

1. [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) defines the shared contracts, models, and service abstractions used by both client-side packages and the server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) APIs.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) provides the core client-side service and orchestration layer that talks to the `/rgf/api/...` endpoints and exposes client runtime services such as API access, security, localization, menus, notifications, and handlers.
3. [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) adapts that client runtime to the Blazor component model with Blazor components, templates, configuration helpers, authentication integration, and JS/resource loading.
4. [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) provides an ApexCharts-based chart implementation for the Blazor integration layer.
5. [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) provides the ready-to-use default Blazor UI layer with concrete menu, dialog, grid, form, filter, toolbar, toast, tree, and chart components.

## Package Summary

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/)
  Shared request/response contracts, models, constants, service interfaces, and infrastructure primitives. This is the common language between the RecroGrid client packages and the server-side Core APIs.
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
  Core client-side runtime layer. Implements API communication and provides DI registration, security, dictionaries, menus, progress, notifications, and higher-level handlers.
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
  Base Blazor integration layer. Exposes the RecroGrid client runtime through Blazor components, templates, auth helpers, dynamic component registration, and JS/resource initialization.
- [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)
  ApexCharts-based chart provider for the Blazor layer. Registers a concrete chart component and transforms RecroGrid aggregation data into ApexCharts series and options.
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/)
  Packaged default UI layer for Blazor applications. Supplies the out-of-the-box Bootstrap-based RecroGrid UI and wires in the default menu, dialog, entity, grid, form, pager, tree, toast, toolbar, and chart implementations.

## Repository Structure

- [src/RGF.Abstraction/README.md](/mnt/c/Work/Recrovit/rgf-client/src/RGF.Abstraction/README.md)
- [src/RGF.Client/README.md](/mnt/c/Work/Recrovit/rgf-client/src/RGF.Client/README.md)
- [src/RGF.Client.Blazor/README.md](/mnt/c/Work/Recrovit/rgf-client/src/RGF.Client.Blazor/README.md)
- [src/RGF.Blazor.RgfApexCharts/README.md](/mnt/c/Work/Recrovit/rgf-client/src/RGF.Blazor.RgfApexCharts/README.md)
- [src/RGF.Client.Blazor.UI/README.md](/mnt/c/Work/Recrovit/rgf-client/src/RGF.Client.Blazor.UI/README.md)

## Related Server Package

- [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/): the server-side RecroGrid Framework Core implementation and the endpoints consumed by the client packages in this repository

## Getting Started

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

- [Quickstart](https://recrogridframework.com/quickstart)
