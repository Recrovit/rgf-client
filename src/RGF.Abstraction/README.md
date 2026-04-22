# RecroGrid Framework Abstraction

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Abstraction.svg?label=RGF.Abstraction)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) [![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Core.svg?label=RGF.Core)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) [![NuGet Version](https://img.shields.io/nuget/v/RecroGrid.svg?label=RecroGrid)](https://www.nuget.org/packages/RecroGrid/) ![NuGet Downloads](https://img.shields.io/nuget/dt/RecroGrid)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) is the shared contract library of the RecroGrid Framework ecosystem.

Its main responsibility is to define the common request/response types, service interfaces, models, constants, and infrastructure primitives that are used by other RGF packages. In practice, this package is the common language between the client-side RecroGrid Framework libraries and the server-side RecroGrid Framework Core APIs.

This relationship is easy to miss when looking only at this project in isolation, but it is central to how the framework works:

- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) implements `IRgfApiService`
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) sends `RgfGridRequest` payloads to the server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) API endpoints under `/rgf/api/...`
- the server responds with shared contract types such as `RgfResult<RgfGridResult>`, `RgfResult<RgfFormResult>`, `RgfResult<RgfFilterResult>`, and related models
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) and [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/) consume these shared contracts to render grids, forms, menus, filters, charts, and security-aware UI behavior

Because both sides rely on the same abstractions, [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) helps keep the client and server aligned on payload shape, metadata, and behavioral conventions.

## What The Package Contains

### API contracts

The `Contracts/API` folder contains the core DTOs used to communicate with the server-side Core APIs, including:

- `RgfSessionParams`
- `RgfGridRequest`
- `RgfResult<T>`
- `RgfGridResult`
- `RgfFormResult`
- `RgfFilterResult`
- `RgfUserState`

These types describe RecroGrid sessions, entity requests, form payloads, filtering, charting, user state, and general operation results.

### Service abstractions

The `Contracts/Services` folder defines service interfaces that higher-level packages implement or consume, for example:

- `IRgfApiService`
- `IRgfMenuService`
- `IRecroSecService`
- `IRecroDictService`
- `IRgfEventNotificationService`

This keeps API access, security checks, dictionary lookup, menu loading, and event notifications decoupled from concrete client implementations.

### Shared models

The `Models` folder contains the metadata and data-shaping types that describe the RecroGrid domain, such as:

- entity and property metadata
- form and filter definitions
- grid, chart, column, and aggregation settings
- menu descriptors
- dynamic payload containers

These models are reused across multiple packages so grid rendering and server responses can evolve together around one contract set.

### Infrastructure and helpers

The package also includes reusable infrastructure elements such as:

- API request/response abstractions
- security-related types
- events and dispatching helpers
- framework constants and header keys
- utility extensions and attributes

## How It Fits Into The RGF Stack

At a high level, the flow looks like this:

1. A client-side handler or component creates a shared request object such as `RgfGridRequest`.
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) serializes and sends that request to a server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) API endpoint.
3. The server processes the request and returns shared response contracts from [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).
4. Client-side packages interpret those contracts to render UI and execute framework behavior.

This makes [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) the interoperability layer of the framework rather than a standalone end-user feature package.

## Typical Consumers

[`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/) is typically referenced by:

- client packages that call the RecroGrid Framework Core APIs
- Blazor/UI packages that render RecroGrid data and metadata
- shared libraries that need access to RecroGrid contract types
- server-side components that want to use the same DTOs and abstractions as the client

## Related Packages

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/): shared contracts, models, and abstractions used across the RecroGrid Framework client and server packages
- [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/): server-side RecroGrid Framework Core implementation and API surface
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): HTTP client implementation and client-side service layer built on the abstractions defined here
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): Blazor integration for consuming RecroGrid contracts
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level UI components built on top of the shared contracts
