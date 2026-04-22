# RecroGrid Framework Blazor ApexCharts

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Blazor.RgfApexCharts.svg?label=Recrovit.RecroGridFramework.Blazor.ApexCharts)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) is the ApexCharts-based chart integration package of the RecroGrid Framework Blazor stack.

Its main responsibility is to provide a concrete chart implementation for [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/), using [`Blazor-ApexCharts`](https://www.nuget.org/packages/Blazor-ApexCharts/) to render chart data produced from RecroGrid aggregation workflows.

In practice, this package is a chart provider for the RecroGrid Blazor layer:

- it registers a concrete chart component for the `Chart` slot used by [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/)
- it transforms aggregated RecroGrid data into ApexCharts series and chart options
- it provides the Blazor UI for chart settings such as series type, grouping, stacking, orientation, palette, theme, and chart dimensions
- it loads the chart-specific JavaScript and stylesheet resources required by the ApexCharts integration
- it extends the core RecroGrid chart experience without changing the shared contracts or the base client runtime

Because of this, [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) is not a standalone charting library. It is a RecroGrid-specific chart implementation package built on top of the Blazor integration layer.

## What The Package Contains

### ApexCharts-based chart components

The package provides the chart UI and rendering components used by the RecroGrid chart workflow, including:

- `ChartComponent`
- `BaseChartComponent`
- `ApexChartComponent`

These components work together with `RgfChartComponent` from [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) to render chart settings, aggregated data views, and the final ApexCharts output.

### Chart data transformation

The package contains the types and logic needed to convert RecroGrid aggregation results into ApexCharts-friendly structures, including:

- `ApexChartSettings`
- `ChartSerie`
- `ChartSerieData`
- palette helpers such as `RgfColorPalettes`

This transformation layer maps grouped and subgrouped RecroGrid data into ApexCharts series, axes, titles, labels, and chart options.

### Chart configuration and registration

`RgfApexChartsConfiguration` is responsible for connecting the package to the RecroGrid Blazor runtime.

It is responsible for:

- registering `ChartComponent` as the active chart component in `RgfBlazorConfiguration`
- loading the package stylesheet bundle
- loading the chart-specific JavaScript module
- unregistering the chart component and unloading resources when needed
- exposing an initialization extension method for application startup

This makes the package plug into the chart extension point of [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/).

### Interactive chart behavior

The package also adds chart-specific runtime behavior such as:

- chart resizing through JS interop
- delayed redraw and refresh behavior
- switching between settings, chart, and data views
- handling chart creation only after valid aggregation settings are provided
- synchronizing chart dimensions and state with the parent RecroGrid chart workflow

This allows chart rendering to stay aligned with the rest of the RecroGrid entity and toolbar experience.

## How It Fits Into The RGF Stack

At a high level, the flow looks like this:

1. [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) provides the shared chart, aggregation, and entity contracts.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) and [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) provide the chart workflow, managers, handlers, and Blazor integration points.
3. [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) registers a concrete chart component implementation for that workflow.
4. Aggregated RecroGrid data is transformed into ApexCharts series and rendered through the `Blazor-ApexCharts` library.

This makes [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) the default ApexCharts-based chart provider for the RecroGrid Blazor stack.

## Typical Consumers

[`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/) is typically referenced by:

- Blazor applications using RecroGrid chart functionality with ApexCharts rendering
- applications that want a ready-made chart component plugged into the RecroGrid Blazor chart slot
- applications that build on [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) and need interactive chart rendering

## Related Packages

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/): shared contracts, models, and abstractions used across the client and server packages
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): client-side API access, runtime services, and orchestration for RecroGrid
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): core Blazor integration layer that defines the chart extension point used here
- [`Recrovit.RecroGridFramework.Blazor.ApexCharts`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Blazor.ApexCharts/): ApexCharts-based chart implementation for the RecroGrid Blazor stack
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level UI components that can build on the Blazor chart integration
- [`Blazor-ApexCharts`](https://www.nuget.org/packages/Blazor-ApexCharts/): third-party charting library used by this package for rendering
