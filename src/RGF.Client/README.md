# RecroGrid Framework Client

[![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Client.svg?label=RGF.Client)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) [![NuGet Version](https://img.shields.io/nuget/v/Recrovit.RecroGridFramework.Core.svg?label=RGF.Core)](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) [![NuGet Version](https://img.shields.io/nuget/v/RecroGrid.svg?label=RecroGrid)](https://www.nuget.org/packages/RecroGrid/) ![NuGet Downloads](https://img.shields.io/nuget/dt/RecroGrid)

Official Website: [RecroGrid Framework](https://RecroGridFramework.com)

## Overview

[`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) is the core client-side service and orchestration layer of the RecroGrid Framework ecosystem.

Its main responsibility is to implement the runtime client behavior that talks to the server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) APIs, using the shared contracts defined in [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).

In practice, this package is the bridge between application code and the `/rgf/api/...` endpoints exposed by the RecroGrid Framework server side:

- it implements `IRgfApiService`
- it serializes shared request contracts such as `RgfGridRequest`
- it calls the server-side RecroGrid Framework Core API endpoints
- it deserializes shared response contracts such as `RgfResult<RgfGridResult>`, `RgfResult<RgfFormResult>`, and `RgfUserState`
- it provides client-side services for security, localization dictionaries, menus, event notifications, and progress reporting
- it registers the required services in dependency injection for higher-level packages

Because of this, [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) is not a UI package by itself. It is the reusable client infrastructure that higher-level packages and UI integrations can build on.

## What The Package Contains

### API client implementation

The central implementation is `ApiService`, which provides the concrete implementation of `IRgfApiService`.

It is responsible for:

- creating and using the configured `HttpClient` instances
- sending authenticated or non-authenticated requests
- attaching RecroGrid client version headers
- handling JSON serialization and deserialization
- mapping HTTP failures into `IRgfApiResponse<T>`
- reacting to `401 Unauthorized` and `403 Forbidden` responses through `IRgfAuthenticationFailureHandler`

### Dependency injection and configuration

`RgfClientConfiguration` wires the package into an application.

It is responsible for:

- reading RecroGrid configuration from `Recrovit:RecroGridFramework`
- resolving API base addresses and proxy base addresses
- configuring authentication mode
- registering the `HttpClient` instances used by the framework
- registering core client services such as `IRgfApiService`, `IRecroSecService`, `IRecroDictService`, and `IRgfMenuService`
- initializing client-side dictionaries and framework logging metadata

This makes the package the main setup point for RecroGrid client services in non-UI and UI-enabled applications alike.

### Client-side framework services

The package contains several higher-level services that build on the API layer:

- `RecroSecService`: loads user state, tracks authentication, resolves roles and permissions, and persists language selection
- `RecroDictService`: loads and caches RecroGrid dictionaries such as `RGF.Language` and `RGF.UI`
- `MenuService`: loads server-driven menu data
- `RgfEventNotificationService`: dispatches framework events to client consumers
- `RgfProgressService`: supports progress reporting during client operations

These services encapsulate common RecroGrid client behaviors so that upper layers do not need to talk to the API directly for every concern.

### Handlers and orchestration

The package also contains handlers that orchestrate RecroGrid operations using the shared contracts and client services, including:

- grid loading
- form loading and saving
- custom function execution
- aggregation and filtering workflows
- compatibility checks between client and server package versions

These handlers are consumed by higher-level packages such as [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/) and [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/).

## How It Fits Into The RGF Stack

At a high level, the flow looks like this:

1. Application code or a higher-level RGF package creates or prepares a shared request object from [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).
2. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) sends the request to a server-side [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/) API endpoint.
3. The server returns shared response contracts defined in [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/).
4. [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) turns those responses into reusable services and workflows for the rest of the client stack.
5. UI-focused packages consume those services to render grids, forms, menus, filters, and security-aware interactions.

This makes [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) the operational client runtime of the framework and the central layer for client-side behavior that can be consumed by different UI integrations.

## Typical Consumers

[`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/) is typically referenced by:

- applications that need typed access to the RecroGrid Framework Core APIs
- higher-level RGF UI packages and other client integrations
- client-side code that needs permission, user state, localization, or menu services
- applications that want the RecroGrid DI setup without directly implementing the API layer themselves

## Related Packages

- [`Recrovit.RecroGridFramework.Abstraction`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Abstraction/): shared contracts, models, and abstractions used by the client and server packages
- [`Recrovit.RecroGridFramework.Core`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Core/): server-side RecroGrid Framework Core implementation and API surface
- [`Recrovit.RecroGridFramework.Client`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client/): client-side API access, framework services, and runtime orchestration
- [`Recrovit.RecroGridFramework.Client.Blazor`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor/): Blazor integration built on top of the client services
- [`Recrovit.RecroGridFramework.Client.Blazor.UI`](https://www.nuget.org/packages/Recrovit.RecroGridFramework.Client.Blazor.UI/): higher-level UI components built on top of the client services and shared contracts
