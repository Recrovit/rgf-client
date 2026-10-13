using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Configuration;

// RGF-DOC: rgf.client.get-started.server-side.create-application
// RGF-DOC: rgf.client.blazor-host-openidconnect.server-side-proxy

/// <summary>
/// High-level ASP.NET Core host setup for RGF SSR server-proxy and Recrovit OpenID Connect.
/// </summary>
public static class RgfBlazorServerProxyOpenIdConnectHostExtensions
{
    private const string RazorComponentsMarkerServiceTypeName = "Microsoft.AspNetCore.Components.Endpoints.RazorComponentsMarkerService";

    /// <summary>
    /// Registers the Razor Components services required by RGF hosts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when Razor Components were already registered by the host.</exception>
    /// <param name="builder">The host builder before any AddRazorComponents registration.</param>
    /// <returns>The Razor Components builder with server/WASM modes and authentication-state serialization.</returns>
    public static IRazorComponentsBuilder AddRgfBlazorServerProxyOpenIdConnectRazorComponents(this WebApplicationBuilder builder)
    {
        if (builder.Services.Any(static descriptor => descriptor.ServiceType.FullName == RazorComponentsMarkerServiceTypeName))
        {
            throw new InvalidOperationException(
                "Razor Components are already registered. Remove the existing AddRazorComponents chain and use AddRgfBlazorServerProxyOpenIdConnectHost instead.");
        }

        return builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
    }

    /// <summary>
    /// Registers the Recrovit OpenID Connect infrastructure together with the RGF SSR server-proxy host services.
    /// </summary>
    /// <param name="builder">The host builder with OIDC and RGF proxy settings.</param>
    /// <returns>The supplied builder.</returns>
    /// <remarks>Register Razor Components separately. SSR proxy clients require API:ProxyBaseAddress.</remarks>
    public static WebApplicationBuilder AddRgfBlazorServerProxyOpenIdConnectHost(this WebApplicationBuilder builder)
    {
        builder.AddRecrovitOpenIdConnectInfrastructure();
        builder.Services.AddRgfBlazorSessionAuthSsrServices(builder.Configuration);
        builder.Services.AddRgfBlazorServerProxyOpenIdConnectHostServices();

        return builder;
    }

    /// <summary>
    /// Maps the standard middleware and endpoints required by the RGF SSR server-proxy OpenID Connect host.
    /// </summary>
    /// <remarks>
    /// This keeps the RGF-specific proxy routes on their established paths while also mapping the reusable
    /// Recrovit OpenID Connect authentication endpoints and the generic downstream proxy endpoints provided by the
    /// underlying host package.
    /// </remarks>
    /// <param name="app">The built host application.</param>
    /// <param name="notFoundPath">Status-code re-execution path; defaults to /not-found.</param>
    /// <returns>The supplied application.</returns>
    public static WebApplication MapRgfBlazorServerProxyOpenIdConnectEndpoints(this WebApplication app, string notFoundPath = "/not-found")
    {
        app.UseRecrovitOpenIdConnectForwardedHeaders();
        app.UseRecrovitOpenIdConnectStatusCodePagesWithReExecute(notFoundPath, null, true);
        app.UseRecrovitOpenIdConnectAuthentication();
        app.UseRecrovitOpenIdConnectProxyTransports();
        app.MapRecrovitOpenIdConnectEndpoints();
        app.MapDownstreamApiProxyEndpoints();
        app.MapRgfProxyEndpoints();

        return app;
    }

    /// <summary>
    /// Maps the standard static assets and interactive Razor component render modes required by RGF hosts.
    /// </summary>
    /// <typeparam name="TRootComponent">The host root Razor component.</typeparam>
    /// <param name="app">The built host application.</param>
    /// <param name="additionalAssemblies">Assemblies containing additional routable components, normally including the Client assembly.</param>
    /// <returns>The mapped component endpoints with both interactive render modes.</returns>
    public static RazorComponentsEndpointConventionBuilder MapRgfBlazorServerProxyOpenIdConnectComponents<TRootComponent>(
        this WebApplication app,
        params Assembly[] additionalAssemblies)
        where TRootComponent : IComponent
    {
        app.MapStaticAssets();

        var builder = app.MapRazorComponents<TRootComponent>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode();

        if (additionalAssemblies.Length > 0)
        {
            builder.AddAdditionalAssemblies(additionalAssemblies);
        }

        return builder;
    }

    private static IServiceCollection AddRgfBlazorServerProxyOpenIdConnectHostServices(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IRgfServerRequestCookieAccessor, HttpContextRgfServerRequestCookieAccessor>());

        return services;
    }

    private sealed class HttpContextRgfServerRequestCookieAccessor(IHttpContextAccessor httpContextAccessor) : IRgfServerRequestCookieAccessor
    {
        public string? GetCookieHeader() => httpContextAccessor.HttpContext?.Request.Headers["Cookie"].ToString();
    }
}
