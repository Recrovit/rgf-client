using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client;

// RGF-DOC: rgf.client.blazor.direct-api-access
// RGF-DOC: rgf.client.blazor-sessionauth.configuration
// RGF-DOC: rgf.client.get-started.wasm.create-application

/// <summary>Process-wide client settings populated during service registration and initialization.</summary>
public class RgfClientConfiguration
{
    /// <summary>Whether client initialization has completed; registration alone does not set this flag.</summary>
    public static bool IsInitialized { get; internal set; } = false;

    /// <summary>Application path prefix for static client resources, without a trailing slash; empty at the site root.</summary>
    public static string AppRootPath { get; internal set; } = string.Empty;

    /// <summary>Configured backend API base address, without a trailing slash.</summary>
    public static string ExternalApiBaseAddress { get; internal set; } = string.Empty;

    /// <summary>Effective HTTP client address after applying override, proxy configuration and auth-mode fallback.</summary>
    public static string ProxyApiBaseAddress { get; internal set; } = string.Empty;

    /// <summary>Authentication mode chosen at registration; defaults to no built-in authentication.</summary>
    public static RgfApiAuthMode ApiAuthMode { get; internal set; } = RgfApiAuthMode.None;

    /// <summary>File version of the client assembly.</summary>
    public static string Version => _version.Value;

    private static readonly Lazy<string> _version = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);

    /// <summary>Version headers contributed by the registered client packages.</summary>
    public static Dictionary<string, string> ClientVersions { get; } = [];

    /// <summary>Minimum compatible Core version; higher-level integrations may raise this requirement.</summary>
    public static Version MinimumRgfCoreVersion = new Version(10, 3, 0);//RGF.Core MinVersion
}

/// <summary>Registers shared client services and initializes dictionary and logging support.</summary>
public static class RgfClientConfigurationExtension
{
    /// <summary>Registers API clients, dictionary, security, menu, events, progress and Recroby capability services.</summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Configuration containing Recrovit:RecroGridFramework settings.</param>
    /// <param name="logger">Optional registration logger; resolved without creating a service provider when omitted.</param>
    /// <param name="authMode">Authentication integration selected by the calling host adapter.</param>
    /// <param name="proxyBaseAddressOverride">First-choice HTTP base address; takes precedence over configured proxy and external addresses.</param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>Proxy modes require an override or API:ProxyBaseAddress. Other modes fall back to API:BaseAddress.
    /// Registration populates process-wide settings; do not use it for independent tenants with different endpoints in one process.
    /// Authentication adapters replace the default no-op token and failure services.</remarks>
    /// <exception cref="InvalidOperationException">The effective API base address is empty.</exception>
    public static IServiceCollection AddRgfServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null,
        RgfApiAuthMode authMode = RgfApiAuthMode.None, string? proxyBaseAddressOverride = null)
    {
        var registrationLogger = RegistrationLoggerResolver.Resolve(services, logger, typeof(RgfClientConfigurationExtension));
        var config = configuration.GetSection("Recrovit:RecroGridFramework");
        var externalBaseAddress = config.GetValue<string>("API:BaseAddress", string.Empty)!.TrimEnd('/');
        var configuredProxyBaseAddress = config.GetValue<string>("API:ProxyBaseAddress", string.Empty);
        var effectiveProxyBaseAddress = ResolveProxyBaseAddress(authMode, externalBaseAddress, configuredProxyBaseAddress, proxyBaseAddressOverride);
        var root = config.GetValue("AppRootPath", config.GetValue("AppRootUrl", ""));
        if (!string.IsNullOrEmpty(root))
        {
            RgfClientConfiguration.AppRootPath = root.TrimEnd('/');
        }
        ApiService.ExternalBaseAddress = externalBaseAddress;
        ApiService.BaseAddress = effectiveProxyBaseAddress;
        RgfClientConfiguration.ExternalApiBaseAddress = externalBaseAddress;
        RgfClientConfiguration.ProxyApiBaseAddress = effectiveProxyBaseAddress;
        RgfClientConfiguration.ApiAuthMode = authMode;

        registrationLogger.LogInformation("AddRgfServices: AppRootPath={AppRootPath}, ProxyApiBaseAddress={ProxyApiBaseAddress}, ExternalApiBaseAddress={ExternalApiBaseAddress}, ApiAuthMode={ApiAuthMode}",
            RgfClientConfiguration.AppRootPath, RgfClientConfiguration.ProxyApiBaseAddress, RgfClientConfiguration.ExternalApiBaseAddress, RgfClientConfiguration.ApiAuthMode);

        if (string.IsNullOrEmpty(ApiService.BaseAddress))
        {
            var msg = authMode is RgfApiAuthMode.ServerProxy or RgfApiAuthMode.ServerProxySsr
                ? "The proxy base address is missing or invalid. Pass the proxyBaseAddress parameter or set 'Recrovit:RecroGridFramework:API:ProxyBaseAddress'."
                : "The 'Recrovit:RecroGridFramework:API:BaseAddress' configuration setting is missing or invalid.";
            registrationLogger.LogCritical(msg);
            throw new InvalidOperationException(msg);
        }

        RgfClientConfiguration.ClientVersions.TryAdd(RgfHeaderKeys.RgfClientVersion, RgfClientConfiguration.Version);

        services.AddHttpClient(ApiService.RgfApiClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(ApiService.BaseAddress);
        });
        services.AddHttpClient(ApiService.RgfAuthApiClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(ApiService.BaseAddress);
        });

        services.AddSingleton<IRgfApiService, ApiService>();
        services.AddScoped<AI.RgfRecrobyCapabilityService>();
        services.AddScoped<IRgfAccessTokenAccessor, NoOpRgfAccessTokenAccessor>();
        services.AddSingleton<IRgfAuthenticationFailureHandler, NoOpRgfAuthenticationFailureHandler>();
        services.AddScoped<IRgfEventNotificationService, RgfEventNotificationService>();
        services.AddScoped<IRecroSecService, RecroSecService>();
        services.AddScoped<IRecroDictService, RecroDictService>();
        services.AddScoped<IRgfMenuService, MenuService>();
        services.AddTransient<IRgfProgressService, RgfProgressService>();

        return services;
    }

    private static string ResolveProxyBaseAddress(RgfApiAuthMode authMode, string externalBaseAddress, string configuredProxyBaseAddress, string? proxyBaseAddressOverride)
    {
        if (!string.IsNullOrWhiteSpace(proxyBaseAddressOverride))
        {
            return proxyBaseAddressOverride.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(configuredProxyBaseAddress))
        {
            return configuredProxyBaseAddress.TrimEnd('/');
        }

        return authMode is RgfApiAuthMode.ServerProxy or RgfApiAuthMode.ServerProxySsr
            ? string.Empty
            : externalBaseAddress;
    }

    /// <summary>Initializes client logging and, for browser rendering, the resource dictionary once per process.</summary>
    /// <param name="serviceProvider">The built provider containing registered client and logging services.</param>
    /// <param name="clientSideRendering">Whether to load the dictionary; false for SSR startup.</param>
    /// <returns>A task completing after initialization.</returns>
    /// <remarks>WASM bearer mode uses the security service's current user language when available.</remarks>
    public static async Task InitializeRgfClientAsync(this IServiceProvider serviceProvider, bool clientSideRendering = true)
    {
        if (!RgfClientConfiguration.IsInitialized)
        {
            RgfLoggerFactory.Initialize(serviceProvider.GetRequiredService<ILoggerFactory>());

            if (clientSideRendering)
            {
                var recroDict = serviceProvider.GetRequiredService<IRecroDictService>();
                string? language = null;
                if (RgfClientConfiguration.ApiAuthMode == RgfApiAuthMode.WasmBearer)
                {
                    language = serviceProvider.GetService<IRecroSecService>()?.UserLanguage;
                }
                await recroDict.InitializeAsync(language);
            }
            var logger = serviceProvider.GetRequiredService<ILogger<RgfClientConfiguration>>();
            logger?.LogInformation("RecroGrid Framework Client v{Version} initialized.", RgfClientConfiguration.Version);
            RgfClientConfiguration.IsInitialized = true;
        }
    }
}
