# RecroGrid Framework Client Blazor

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.svg?label=RGF.Client.Blazor)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) [![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Core.svg?label=RGF.Core)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) [![NuGet Version](https://img.shields.io/nuget/v/RecroGrid.svg?label=RecroGrid)](https://www.nuget.org/packages/RecroGrid/) ![NuGet Downloads](https://img.shields.io/nuget/dt/RecroGrid)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) is the Blazor integration layer of the RecroGrid Framework ecosystem.

Its main responsibility is to turn the client-side services provided by [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) into reusable Blazor components, templates, configuration helpers, and runtime services, using the shared contracts from [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).

In practice, this package is the layer that makes RecroGrid usable inside Blazor applications:

- it provides the core Blazor components for rendering RecroGrid entities, grids, forms, filters, charts, toolbars, dialogs, pagers, and trees
- it connects Blazor component lifecycle and templating to the client-side managers and services from [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
- it adds Blazor-specific dependency injection and authentication registration helpers
- it integrates JavaScript interop, static resources, and framework stylesheets into the Blazor runtime
- it offers dynamic component registration so applications can plug in their own menu, dialog, chart, or entity components

Because of this, [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) is the package that adapts the RecroGrid client runtime to the Blazor component model.

## What The Package Contains

### Blazor components

The `Components` folder contains the main Blazor building blocks of the framework, including:

- `RgfEntityComponent`
- `RgfGridComponent`
- `RgfFormComponent`
- `RgfFilterComponent`
- `RgfChartComponent`
- `RgfToolbarComponent`
- `RgfTreeComponent`
- `RgfDynamicDialog`
- `RgfPagerComponent`

These components provide the base rendering and interaction model for RecroGrid features inside Blazor applications.

The package also includes smaller helper components that support the base Blazor integration surface.

### Blazor configuration and registration

`RgfBlazorConfiguration` is the main entry point for wiring the package into a Blazor application.

It is responsible for:

- extending the base service registration from [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/)
- registering Blazor-specific authentication modes
- configuring WebAssembly bearer-token scenarios
- loading JavaScript modules, stylesheet resources, and server-provided script references
- registering application-specific entity, menu, dialog, and chart components

This makes the package the primary setup point for RecroGrid in Blazor applications.

### Authentication and session integration

The package contains Blazor-specific authentication helpers and runtime services such as:

- `RgfAuthorizationMessageHandler`

For route-aware session authentication scenarios, use the dedicated [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/) package.

### Parameters, templates, and dynamic composition

The package includes parameter and template types that allow applications to customize rendering and compose dynamic UI pieces, for example:

- `RgfEntityParameters`
- `RgfFormParameters`
- `RgfGridParameters`
- `RgfDialogParameters`
- `RgfTreeNodeParameters`

These types make it possible to plug custom `RenderFragment` templates and component mappings into the framework while still using the underlying RecroGrid client services.

### JavaScript interop and resource loading

The package uses Blazor JS interop to load and coordinate client-side resources needed by the framework, including:

- RecroGrid JavaScript modules
- server-provided script references
- RecroGrid stylesheets
- jQuery and jQuery UI related assets when required by the client runtime

This allows the Blazor layer to activate the interactive client-side behavior expected by RecroGrid components.

## How It Fits Into The RGF Stack

At a high level, the flow looks like this:

1. [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) provides the shared contracts and data models.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) provides the client-side API access, orchestration, security, localization, and handler services.
3. [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) exposes those capabilities through Blazor components, templates, JS interop, and Blazor-specific service registration.
4. Higher-level UI packages and application-specific components build on this Blazor layer to provide final user-facing experiences.

This makes [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) the foundational Blazor package of the RecroGrid client stack.

## Typical Consumers

[`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) is typically referenced by:

- Blazor applications that want to use RecroGrid runtime features directly
- higher-level RecroGrid UI packages
- applications that want to customize entity, dialog, menu, chart, or form rendering through Blazor templates and components
- applications that need Blazor-specific authentication and session integration for RecroGrid

## Related Packages

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/): shared contracts, models, and abstractions used across the client and server packages
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): client-side API access, runtime services, and orchestration used by the Blazor layer
- [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/): server-side RecroGrid Framework Core implementation and API surface
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): core Blazor integration for the RecroGrid client stack
- [`Recrovit.RecroGridFramework.Client.Blazor.SessionAuth`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.SessionAuth/): route-aware session authentication integration for Blazor clients and SSR hosts
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level UI components built on top of the Blazor integration layer
