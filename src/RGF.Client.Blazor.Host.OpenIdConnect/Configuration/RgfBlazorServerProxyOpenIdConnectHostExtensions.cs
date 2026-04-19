using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recrovit.AspNetCore.Authentication.OpenIdConnect.Configuration;
using Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Proxy;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.Host.OpenIdConnect.Configuration;

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
    public static WebApplicationBuilder AddRgfBlazorServerProxyOpenIdConnectHost(this WebApplicationBuilder builder)
    {
        builder.AddRecrovitOpenIdConnectInfrastructure();
        builder.Services.AddRgfBlazorServerProxySsrServices(builder.Configuration);
        builder.Services.AddRgfBlazorServerProxyOpenIdConnectHostServices();

        return builder;
    }

    /// <summary>
    /// Maps the standard middleware and endpoints required by the RGF SSR server-proxy OpenID Connect host.
    /// </summary>
    public static WebApplication MapRgfBlazorServerProxyOpenIdConnectEndpoints(this WebApplication app, string notFoundPath = "/not-found")
    {
        app.UseRecrovitOpenIdConnectForwardedHeaders();
        app.UseRecrovitOpenIdConnectStatusCodePagesWithReExecute(notFoundPath, null, true);
        app.UseRecrovitOpenIdConnectAuthentication();
        app.UseRecrovitOpenIdConnectProxyTransports();
        app.MapRecrovitOpenIdConnectEndpoints();
        app.MapRgfProxyEndpoints();

        return app;
    }

    /// <summary>
    /// Maps the standard static assets and interactive Razor component render modes required by RGF hosts.
    /// </summary>
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
