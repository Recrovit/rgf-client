using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Recrovit.AspNetCore.Components.Routing.Configuration;
using Recrovit.AspNetCore.Components.Routing.Models;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Initialization;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.PrincipalSync;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.Session;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authentication.StateProvider;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Authorization.RouteAccess;
using Recrovit.RecroGridFramework.Client.Blazor.SessionAuth.Proxy;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.SessionAuth;

// RGF-DOC: rgf.client.blazor-sessionauth.configuration
// RGF-DOC: rgf.client.get-started.server-side.configure-client
// RGF-DOC: rgf.client.blazor-host-openidconnect.server-side-proxy

/// <summary>Registers host-session authentication adapters for interactive clients and SSR hosts.</summary>
public static class RgfBlazorSessionAuthConfigurationExtensions
{
    private const string SsrProxyClientName = "Recrovit.RGF.Blazor.SessionAuth.ServerProxy";
    /// <summary>Named HTTP client used for requests to the current host API.</summary>
    public const string HostApiClientName = "Recrovit.RGF.Blazor.SessionAuth.HostApi";
    /// <summary>Named HTTP client used for downstream API requests through the host proxy.</summary>
    public const string DownstreamProxyClientName = "Recrovit.RGF.Blazor.SessionAuth.DownstreamProxy";

    /// <summary>Registers host-proxy clients, session monitoring, principal synchronization and Client route authorization wrapping.</summary>
    /// <param name="services">The interactive client service collection.</param>
    /// <param name="configuration">RGF proxy settings and principal name-claim fallback configuration.</param>
    /// <param name="apiBaseAddressOverride">Optional host base address, taking precedence over API:ProxyBaseAddress.</param>
    /// <param name="logger">Optional registration logger.</param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>Uses ServerProxy mode. Authentication-state deserialization is decorated with session-aware state.
    /// The route wrapper is supplied only when Client FoundContent is not already configured. Use RecrovitRoutes for it to apply.</remarks>
    /// <exception cref="InvalidOperationException">Neither a proxy address nor an override is configured.</exception>
    public static IServiceCollection AddRgfBlazorSessionAuthClientServices(this IServiceCollection services, IConfiguration configuration, string? apiBaseAddressOverride = null, ILogger? logger = null)
    {
        logger = ResolveRegistrationLogger(services, logger);
        AddRgfBlazorSessionAuthCoreServices(services, configuration, logger, RgfApiAuthMode.ServerProxy, apiBaseAddressOverride);
        ConfigureClientRoutingFoundContent(services);
        ConfigureAuthenticationPrincipalSynchronization(services, configuration);
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddAuthenticationStateDeserialization();
        services.AddSingleton<IRgfAuthenticationSessionMonitor, RgfAuthenticationSessionMonitor>();
        services.AddSingleton<RgfAuthenticationPrincipalSnapshotSynchronizer>();
        services.AddSingleton<IRgfAuthenticationFailureHandler>(serviceProvider => serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>());
        services.DecorateAuthenticationStateProvider();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRgfBlazorInitializationHook, RgfSessionAuthInitializationHook>());
        logger.LogInformation("RecroGrid Framework Blazor SessionAuth registration: client session-auth via configured host-backed API base address.");
        return services;
    }

    /// <summary>Registers SSR host-proxy clients and cookie-forwarding HTTP handlers.</summary>
    /// <param name="services">The SSR host service collection.</param>
    /// <param name="configuration">RGF settings containing API:ProxyBaseAddress.</param>
    /// <param name="logger">Optional registration logger.</param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>Uses ServerProxySsr mode. The default cookie accessor and session monitor are no-ops;
    /// the OIDC host integration supplies an HTTP-context cookie accessor. This method does not register OIDC or Razor Components.</remarks>
    /// <exception cref="InvalidOperationException">The proxy base address is missing.</exception>
    public static IServiceCollection AddRgfBlazorSessionAuthSsrServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null)
    {
        logger = ResolveRegistrationLogger(services, logger);
        services.TryAddScoped<IRgfServerRequestCookieAccessor, NoOpRgfServerRequestCookieAccessor>();
        services.AddTransient<RgfServerProxyAuthCookieHandler>();
        services.TryAddSingleton<IRgfAuthenticationSessionMonitor, NoOpRgfAuthenticationSessionMonitor>();

        AddRgfBlazorSessionAuthCoreServices(services, configuration, logger, RgfApiAuthMode.ServerProxySsr);
        ConfigureServerProxySsrHttpClients(services, logger);
        logger.LogInformation("RecroGrid Framework Blazor SessionAuth registration: SSR session-auth via configured host-backed API base address.");
        return services;
    }

    private static void AddRgfBlazorSessionAuthCoreServices(IServiceCollection services, IConfiguration configuration, ILogger logger, RgfApiAuthMode authMode, string? apiBaseAddressOverride = null)
    {
        services.AddRgfServices(configuration, logger, authMode, apiBaseAddressOverride);
        services.TryAddSingleton<RgfAuthenticationEndpointResolver>();
        services.AddHttpClient(HostApiClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(ApiService.BaseAddress);
        });
        services.TryAddScoped<IHostApiInvoker, SessionAuthHostApiInvoker>();
        services.AddHttpClient(DownstreamProxyClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(ApiService.BaseAddress);
        });
        services.TryAddScoped<IDownstreamApiInvoker, SessionAuthDownstreamApiInvoker>();

        if (RgfClientConfiguration.ClientVersions.TryAdd(RgfHeaderKeys.RgfClientBlazorVersion, RgfBlazorConfiguration.Version))
        {
            RgfClientConfiguration.ClientVersions.Remove(RgfHeaderKeys.RgfClientVersion);
        }

        if (RgfClientConfiguration.MinimumRgfCoreVersion < RgfBlazorConfiguration.MinimumRgfCoreVersion)
        {
            RgfClientConfiguration.MinimumRgfCoreVersion = RgfBlazorConfiguration.MinimumRgfCoreVersion;
        }
    }

    private static void ConfigureAuthenticationPrincipalSynchronization(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RgfAuthenticationOptions>()
            .Bind(configuration.GetSection("Recrovit:RecroGridFramework").GetSection("Authentication"));
        services.TryAddSingleton<RgfAuthenticationPrincipalFactory>();
    }

    private static void ConfigureClientRoutingFoundContent(IServiceCollection services)
    {
        services.PostConfigure<RecrovitRoutingOptions>(options =>
        {
            if (options.GetFoundContent(RecrovitRoutesKind.Client) is null)
            {
                options.SetFoundContent(RecrovitRoutesKind.Client, RenderClientRoute);
            }
        });
    }

    private static RenderFragment RenderClientRoute(RecrovitFoundContentContext context) => builder =>
    {
        builder.OpenComponent<RgfAuthorizeRouteContent>(0);
        builder.AddAttribute(1, nameof(RgfAuthorizeRouteContent.RouteData), context.RouteData);
        builder.AddAttribute(2, nameof(RgfAuthorizeRouteContent.DefaultContent), context.DefaultContent);
        builder.CloseComponent();
    };

    private static void ConfigureServerProxySsrHttpClients(IServiceCollection services, ILogger logger)
    {
        logger.LogInformation("RecroGrid Framework Blazor SessionAuth registration: SSR handler '{AuthorizationMessageHandlerTypeName}' attached to RGF HTTP clients.", nameof(RgfServerProxyAuthCookieHandler));

        services.AddHttpClient(SsrProxyClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(ApiService.BaseAddress);
        });

        foreach (var clientName in new[] { ApiService.RgfApiClientName, ApiService.RgfAuthApiClientName, SsrProxyClientName, HostApiClientName, DownstreamProxyClientName })
        {
            services.Configure<HttpClientFactoryOptions>(clientName, httpClientOptions =>
            {
                httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<RgfServerProxyAuthCookieHandler>());
                });
            });
            services.PostConfigure<HttpClientFactoryOptions>(clientName, httpClientOptions =>
            {
                // Apply after normal primary-handler registrations, preserving the configured transport.
                httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    if (OperatingSystem.IsBrowser())
                    {
                        throw new PlatformNotSupportedException("SSR cookie-forwarding HTTP clients are not supported in the browser.");
                    }

                    var transport = builder.PrimaryHandler;
                    var visited = new HashSet<HttpMessageHandler>();
                    while (transport is DelegatingHandler delegating && visited.Add(transport))
                    {
                        transport = delegating.InnerHandler;
                    }

                    switch (transport)
                    {
                        case SocketsHttpHandler sockets:
                            sockets.AllowAutoRedirect = false;
                            sockets.UseCookies = false;
                            break;
                        case HttpClientHandler client:
                            client.AllowAutoRedirect = false;
                            client.UseCookies = false;
                            break;
                        default:
                            throw new InvalidOperationException($"SSR cookie-forwarding HTTP client '{clientName}' requires an inspectable SocketsHttpHandler or HttpClientHandler transport with automatic redirects and cookies disabled. Actual transport: {transport?.GetType().FullName ?? "null"}.");
                    }
                });
            });
        }
    }

    private static ILogger ResolveRegistrationLogger(IServiceCollection services, ILogger? logger) =>
        RegistrationLoggerResolver.Resolve(services, logger, typeof(RgfBlazorSessionAuthConfigurationExtensions));
}
