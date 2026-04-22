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

public static class RgfBlazorSessionAuthConfigurationExtensions
{
    private const string SsrProxyClientName = "Recrovit.RGF.Blazor.SessionAuth.ServerProxy";

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

        foreach (var clientName in new[] { ApiService.RgfApiClientName, ApiService.RgfAuthApiClientName, SsrProxyClientName })
        {
            services.Configure<HttpClientFactoryOptions>(clientName, httpClientOptions =>
            {
                httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<RgfServerProxyAuthCookieHandler>());
                });
            });
        }
    }

    private static ILogger ResolveRegistrationLogger(IServiceCollection services, ILogger? logger) =>
        RegistrationLoggerResolver.Resolve(services, logger, typeof(RgfBlazorSessionAuthConfigurationExtensions));
}
