# RecroGrid Framework Client Blazor UI

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.Blazor.UI.svg?label=Recrovit.RecroGridFramework.Client.Blazor.UI)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) is the ready-to-use UI layer of the RecroGrid Framework Blazor stack.

Its main responsibility is to provide a concrete, opinionated set of Blazor UI components, styles, and resource loading conventions on top of [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) and [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/), using the shared contracts from [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).

In practice, this package is the layer that gives RecroGrid a complete default UI in Blazor applications:

- it provides concrete implementations for the menu, dialog, entity, grid, filter, form, pager, tree, toast, toolbar, and chart UI
- it wires those components into the extension points defined by [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
- it ships the supporting Bootstrap and RecroGrid-specific UI resources needed by the default UI layer
- it adds a root component and helper base controls for building consistent RecroGrid user interfaces
- it initializes the default chart implementation from [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)

Because of this, [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) is the package that turns the lower-level RecroGrid Blazor runtime into a complete out-of-the-box user interface.

## What The Package Contains

### Ready-made UI components

The `Components` folder contains the concrete UI implementations that sit on top of the base Blazor integration layer, including:

- `EntityComponent`
- `GridComponent`
- `FormComponent`
- `FilterComponent`
- `PagerComponent`
- `TreeComponent`
- `MenuComponent`
- `DialogComponent`
- `ToolbarComponent`
- `ToastComponent`
- `ChartComponent`
- `RgfRootComponent`

These components provide the default visual and interaction model for working with RecroGrid entities in Blazor applications.

### Base input and layout controls

The package also includes reusable UI primitives under `Components/Base`, such as:

- `RgfButton`
- `RgfCheckBox`
- `RgfComboBox`
- `RgfInput`
- `RgfInputText`
- `RgfInputNumber`
- `RgfInputDate`
- `RgfListBox`
- `RgfSplitter`
- `SpinnerComponent`

These controls are used internally by the packaged UI, and can also help applications stay aligned with the default RecroGrid look and feel.

### Default component registration

`RGFClientBlazorUIConfiguration` is responsible for wiring the packaged UI into the RecroGrid Blazor runtime.

It is responsible for:

- registering `MenuComponent` as the active menu component
- registering `DialogComponent` as the active dialog component
- registering `EntityComponent` as the default entity component
- initializing the ApexCharts-based chart implementation
- loading the UI resource set required by the packaged components

This makes the package the default UI composition layer for RecroGrid in Blazor applications.

### Resource loading and theming

The package loads and coordinates the resources required by the default UI layer, including:

- Bootstrap styles and scripts
- Bootstrap Icons
- jQuery support
- RecroGrid-specific stylesheet bundles
- additional script references returned by the server
- theme selection through the document `data-bs-theme` attribute

This gives the packaged UI a consistent visual and behavioral foundation without requiring every application to wire those assets manually.

### Notifications, dialogs, and application shell behavior

The packaged UI also includes higher-level application behavior, for example:

- `RgfRootComponent` as a root wrapper for RecroGrid UI composition
- `ToastComponent` for framework notifications and background progress feedback
- `DialogComponent` for modal and inline dialog presentation
- menu and toolbar components for framework-driven navigation and actions

These pieces make the package useful not only as a control library, but as the default end-user UI layer of the framework.

## How It Fits Into The RGF Stack

At a high level, the flow looks like this:

1. [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) provides the shared contracts and models.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) provides the client-side API access, services, and orchestration.
3. [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) provides the Blazor integration points, base components, and runtime services.
4. [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) provides the concrete chart implementation.
5. [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) assembles those pieces into a complete default UI layer.

This makes [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) the highest-level packaged UI layer in the current RecroGrid client stack.

## Typical Consumers

[`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) is typically referenced by:

- Blazor applications that want a ready-made RecroGrid UI instead of composing every component manually
- applications that want the default menu, dialog, toast, form, grid, and toolbar implementations
- applications that want the RecroGrid Bootstrap-based visual layer and bundled resource setup
- applications that build on the RecroGrid Blazor stack but prefer starting from the packaged UI components

## Related Packages

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/): shared contracts, models, and abstractions used across the client and server packages
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): client-side API access, runtime services, and orchestration for RecroGrid
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): base Blazor integration layer and extension points used by the packaged UI
- [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/): ApexCharts-based chart implementation used by the packaged UI
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): complete default Blazor UI layer for the current RecroGrid client stack

## Getting Started

- [Quickstart](https://RecroGridFramework.com/quickstart)
