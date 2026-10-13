using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Handlers;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor;

// RGF-DOC: rgf.client.blazor.wasm-authentication
// RGF-DOC: rgf.client.get-started.wasm.create-application
// RGF-DOC: rgf.client.get-started.server-side.configure-client
// RGF-DOC: rgf.client.blazor-ui.entity-ui

/// <summary>Process-wide component templates and resource/version settings for Blazor integration.</summary>
public class RgfBlazorConfiguration
{
    internal static Dictionary<string, Type> EntityComponentTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<ComponentType, Type> ComponentTypes { get; } = [];

    /// <summary>Registers an entity-specific component; an empty entity name selects the fallback and registrations replace earlier values.</summary>
    public static void RegisterEntityComponent<TComponent>(string entityName) where TComponent : ComponentBase
    {
        EntityComponentTypes[entityName ?? string.Empty] = typeof(TComponent);
    }

    /// <summary>Clears all entity registrations, including the fallback supplied by UI initialization.</summary>
    public static void ClearEntityComponentTypes() => EntityComponentTypes.Clear();

    /// <summary>Registers or replaces the implementation used for a shared component kind.</summary>
    public static void RegisterComponent<TComponent>(ComponentType type) where TComponent : ComponentBase
    {
        ComponentTypes[type] = typeof(TComponent);
    }

    /// <summary>Removes the implementation for a shared component kind.</summary>
    public static void UnregisterComponent(ComponentType type) => ComponentTypes.Remove(type);

    /// <summary>Gets a registered component implementation; throws NotImplementedException when the kind has no registration.</summary>
    public static Type GetComponentType(ComponentType type)
    {
        if (!ComponentTypes.TryGetValue(type, out Type? componentType))
        {
            throw new NotImplementedException($"The {type} template component is missing.");
        }
        return componentType;
    }

    /// <summary>Looks up a component implementation without throwing for a missing registration.</summary>
    public static bool TryGetComponentType(ComponentType type, out Type? componentType) => ComponentTypes.TryGetValue(type, out componentType);

    /// <summary>Shared component slots resolved by the Blazor client.</summary>
    public enum ComponentType
    {
        Menu = 1,
        Dialog = 2,
        Chart = 3,
        DashboardPage = 4,
        LoadingIndicator = 5,
    }

    /// <summary>JavaScript namespace used by the base Blazor interop helpers.</summary>
    public const string JsBlazorNamespace = "Recrovit.RGF.Blazor.Client";

    /// <summary>jQuery UI version expected by the bundled client assets.</summary>
    public const string JQueryUiVer = "1.14.1";

    /// <summary>Compares the loaded jQuery UI version with the bundled version through the LPUtils JavaScript helper.</summary>
    public static ValueTask<int> ChkJQueryUiVer(IJSRuntime jsRuntime) => jsRuntime.InvokeAsync<int>("Recrovit.LPUtils.CompareJQueryUIVersion", JQueryUiVer);

    /// <summary>File version of the Blazor client assembly.</summary>
    public static string Version => _version.Value;

    private static readonly Lazy<string> _version = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);

    /// <summary>Minimum Core version required by this Blazor integration.</summary>
    public static Version MinimumRgfCoreVersion { get; } = new Version(10, 3, 0);//RGF.Core MinVersion
}

/// <summary>Registers Blazor authentication adapters and initializes client resources.</summary>
public static class RgfBlazorConfigurationExtension
{
    private static readonly string _executingAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

    /// <summary>Registers the WASM bearer integration; use AddRgfBlazorWasmBearerServices in new applications.</summary>
    [Obsolete("Use AddRgfBlazorWasmBearerServices for Blazor WebAssembly with bearer tokens.")]
    public static IServiceCollection AddRgfBlazorServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null, Type? authorizationMessageHandlerType = null) =>
        services.AddRgfBlazorWasmBearerServices(configuration, logger, authorizationMessageHandlerType);

    /// <summary>Registers RGF clients in WASM bearer mode with a token accessor and authenticated HTTP handler.</summary>
    /// <param name="services">The service collection configured with Blazor OIDC authentication.</param>
    /// <param name="configuration">RGF API address, scopes and resource configuration.</param>
    /// <param name="logger">Optional registration logger.</param>
    /// <param name="authorizationMessageHandlerType">Optional registered DelegatingHandler type. Null or an incompatible type selects the default RGF handler.</param>
    /// <returns>The supplied service collection.</returns>
    /// <remarks>Register OIDC separately. The default handler uses API:DefaultScopes, falling back to openid and profile;
    /// supply the API's delegated scopes. A compatible custom handler must be registered in DI.</remarks>
    /// <exception cref="InvalidOperationException">No effective API base address is configured.</exception>
    public static IServiceCollection AddRgfBlazorWasmBearerServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null, Type? authorizationMessageHandlerType = null)
    {
        logger = ResolveRegistrationLogger(services, logger);
        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.WasmBearer);
        services.AddScoped<IRgfAccessTokenAccessor, WasmRgfAccessTokenAccessor>();
        ConfigureWasmAuthHttpClient(services, authorizationMessageHandlerType, logger);
        return services;
    }

    private static void ConfigureWasmAuthHttpClient(IServiceCollection services, Type? authorizationMessageHandlerType, ILogger logger)
    {
        if (authorizationMessageHandlerType == null || !typeof(DelegatingHandler).IsAssignableFrom(authorizationMessageHandlerType))
        {
            services.AddTransient<RgfAuthorizationMessageHandler>();
            authorizationMessageHandlerType = typeof(RgfAuthorizationMessageHandler);
        }

        logger.LogInformation("RecroGrid Framework Blazor registration: WebAssembly bearer auth with handler '{AuthorizationMessageHandlerTypeName}'.", authorizationMessageHandlerType.Name);

        services.Configure<HttpClientFactoryOptions>(ApiService.RgfAuthApiClientName, httpClientOptions =>
        {
            httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add((DelegatingHandler)builder.Services.GetRequiredService(authorizationMessageHandlerType));
            });
        });
    }

    /// <summary>Registers Blazor clients without a built-in bearer or host-session adapter.</summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">RGF API and resource configuration.</param>
    /// <param name="logger">Optional registration logger.</param>
    /// <returns>The supplied collection.</returns>
    /// <remarks>The application owns any authentication required by its backend.</remarks>
    public static IServiceCollection AddRgfBlazorWithoutAuthServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null)
    {
        logger = ResolveRegistrationLogger(services, logger);
        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.None);
        logger.LogInformation("RecroGrid Framework Blazor registration: without built-in authentication handling.");
        return services;
    }

    private static IServiceCollection AddRgfBlazorServicesCore(IServiceCollection services, IConfiguration configuration, ILogger logger,
        RgfApiAuthMode authMode, string? proxyBaseAddressOverride = null)
    {
        services.AddRgfServices(configuration, logger, authMode, proxyBaseAddressOverride);
        services.TryAddSingleton<RgfAuthenticationEndpointResolver>();

        if (RgfClientConfiguration.ClientVersions.TryAdd(RgfHeaderKeys.RgfClientBlazorVersion, RgfBlazorConfiguration.Version))
        {
            RgfClientConfiguration.ClientVersions.Remove(RgfHeaderKeys.RgfClientVersion);
        }

        if (RgfClientConfiguration.MinimumRgfCoreVersion < RgfBlazorConfiguration.MinimumRgfCoreVersion)
        {
            RgfClientConfiguration.MinimumRgfCoreVersion = RgfBlazorConfiguration.MinimumRgfCoreVersion;
        }

        return services;
    }

    /// <summary>Initializes client logging and Blazor hooks for SSR without browser resource loading.</summary>
    /// <param name="serviceProvider">The built application provider.</param>
    /// <returns>The initialization task.</returns>
    public static Task InitializeRgfBlazorServerAsync(this IServiceProvider serviceProvider) => serviceProvider.InitializeRgfBlazorAsync(false);

    /// <summary>Initializes the shared client, registered Blazor hooks and optional browser resources.</summary>
    /// <param name="serviceProvider">The built provider containing client services.</param>
    /// <param name="clientSideRendering">True to initialize the dictionary and load resources using JavaScript; false for SSR startup.</param>
    /// <returns>A task completing after hooks and resource loading.</returns>
    /// <remarks>Hooks run on each call. Browser loading requires usable JS interop and backend resource endpoints.</remarks>
    public static async Task InitializeRgfBlazorAsync(this IServiceProvider serviceProvider, bool clientSideRendering = true)
    {
        await serviceProvider.InitializeRgfClientAsync(clientSideRendering);

        foreach (var initializationHook in serviceProvider.GetServices<IRgfBlazorInitializationHook>())
        {
            await initializationHook.InitializeAsync(serviceProvider, clientSideRendering, CancellationToken.None);
        }

        if (clientSideRendering)
        {
            await LoadResourcesAsync(serviceProvider);
        }
        var logger = serviceProvider.GetRequiredService<ILogger<RgfBlazorConfiguration>>();
        logger?.LogInformation("RecroGrid Framework Blazor v{version} initialized.", RgfBlazorConfiguration.Version);
    }

    /// <summary>Loads jQuery, backend script references and base Blazor styles through browser JS interop; requires registered API and JS services.</summary>
    public static async Task LoadResourcesAsync(IServiceProvider serviceProvider)
    {
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();

        bool jquery = await jsRuntime.InvokeAsync<bool>("eval", "typeof jQuery != 'undefined'");
        if (!jquery)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/lib/jquery/jquery.min.js");
        }

        if (!SriptReferences.Any())
        {
            var api = serviceProvider.GetRequiredService<IRgfApiService>();
            var res = await api.GetAsync<string[]>("/rgf/api/RGFSriptReferences", authClient: false);
            if (res.Success)
            {
                SriptReferences = res.Result;
                foreach (var item in SriptReferences)
                {
                    await jsRuntime.InvokeAsync<IJSObjectReference>("import", ApiService.BaseAddress + item);
                }
                await jsRuntime.InvokeVoidAsync($"Recrovit.WebCli.SetBaseAddress", ApiService.BaseAddress);
            }
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/scripts/" +
#if DEBUG
                "recrovit-rgf-blazor.js"
#else
            "recrovit-rgf-blazor.min.js"
#endif
                );

            await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetRgfCoreCssHref(), false, RgfCoreCssId);
        }

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded", "rgf-check-stylesheet-client-blazor", "<div class=\"rgf-check-stylesheet-client-blazor\" rgf-wrapper-comp=\"\">",
            GetBundleCssHref(), BlazorCssLib);
    }

    /// <summary>DOM identifier used for the backend Core stylesheet.</summary>
    public const string RgfCoreCssId = "rgf-core-css";

    /// <summary>DOM identifier used for the base Blazor CSS bundle.</summary>
    public const string BlazorCssLib = "rgf-client-blazor-lib";

    /// <summary>Returns the Core stylesheet URL at the effective API address.</summary>
    public static string GetRgfCoreCssHref() => $"{ApiService.BaseAddress}/rgf/resource/RgfCore.css";

    /// <summary>Returns the versioned Blazor CSS bundle URL beneath AppRootPath.</summary>
    public static string GetBundleCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/{_executingAssemblyName}.bundle.scp.css?v={RgfBlazorConfiguration.Version}";

    /// <summary>Returns the bundled jQuery UI stylesheet URL beneath AppRootPath.</summary>
    public static string GetJQueryUiCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/lib/jqueryui/themes/base/jquery-ui.min.css";

    /// <summary>Backend script reference URLs retained after successful resource lookup.</summary>
    public static IEnumerable<string> SriptReferences { get; private set; } = [];

    private static ILogger ResolveRegistrationLogger(IServiceCollection services, ILogger? logger) =>
        RegistrationLoggerResolver.Resolve(services, logger, typeof(RgfBlazorConfigurationExtension));
}
