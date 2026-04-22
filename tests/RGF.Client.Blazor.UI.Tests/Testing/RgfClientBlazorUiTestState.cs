using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Client;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.UI;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal static class RgfClientBlazorUiTestState
{
    private static readonly FieldInfo UiScriptsLoadedField = typeof(RGFClientBlazorUIConfiguration)
        .GetField("_scriptsLoaded", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RGFClientBlazorUIConfiguration._scriptsLoaded.");

    private static readonly PropertyInfo UiThemeNameProperty = typeof(RGFClientBlazorUIConfiguration)
        .GetProperty("ThemeName", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RGFClientBlazorUIConfiguration.ThemeName.");

    private static readonly PropertyInfo AppRootPathProperty = typeof(RgfClientConfiguration)
        .GetProperty(nameof(RgfClientConfiguration.AppRootPath), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException("Unable to locate RgfClientConfiguration.AppRootPath.");

    private static readonly PropertyInfo ExternalApiBaseAddressProperty = typeof(RgfClientConfiguration)
        .GetProperty(nameof(RgfClientConfiguration.ExternalApiBaseAddress), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException("Unable to locate RgfClientConfiguration.ExternalApiBaseAddress.");

    private static readonly PropertyInfo ProxyApiBaseAddressProperty = typeof(RgfClientConfiguration)
        .GetProperty(nameof(RgfClientConfiguration.ProxyApiBaseAddress), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException("Unable to locate RgfClientConfiguration.ProxyApiBaseAddress.");

    private static readonly PropertyInfo ApiAuthModeProperty = typeof(RgfClientConfiguration)
        .GetProperty(nameof(RgfClientConfiguration.ApiAuthMode), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException("Unable to locate RgfClientConfiguration.ApiAuthMode.");

    private static readonly PropertyInfo IsInitializedProperty = typeof(RgfClientConfiguration)
        .GetProperty(nameof(RgfClientConfiguration.IsInitialized), BindingFlags.Static | BindingFlags.Public)
        ?? throw new InvalidOperationException("Unable to locate RgfClientConfiguration.IsInitialized.");

    private static readonly FieldInfo BlazorScriptReferencesField = typeof(RgfBlazorConfigurationExtension)
        .GetField("<SriptReferences>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RgfBlazorConfigurationExtension.SriptReferences.");

    private static readonly PropertyInfo BlazorEntityComponentTypesProperty = typeof(RgfBlazorConfiguration)
        .GetProperty("EntityComponentTypes", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RgfBlazorConfiguration.EntityComponentTypes.");

    public static void Reset()
    {
        UiScriptsLoadedField.SetValue(null, false);
        UiThemeNameProperty.SetValue(null, "light");

        IsInitializedProperty.SetValue(null, false);
        AppRootPathProperty.SetValue(null, string.Empty);
        ExternalApiBaseAddressProperty.SetValue(null, string.Empty);
        ProxyApiBaseAddressProperty.SetValue(null, string.Empty);
        ApiAuthModeProperty.SetValue(null, RgfApiAuthMode.None);
        RgfClientConfiguration.MinimumRgfCoreVersion = new Version(10, 0, 0);
        RgfClientConfiguration.ClientVersions.Clear();

        ApiService.BaseAddress = string.Empty;
        ApiService.ExternalBaseAddress = string.Empty;

        foreach (var componentType in Enum.GetValues<RgfBlazorConfiguration.ComponentType>())
        {
            RgfBlazorConfiguration.UnregisterComponent(componentType);
        }

        RgfBlazorConfiguration.ClearEntityComponentTypes();
        BlazorScriptReferencesField.SetValue(null, Array.Empty<string>());
    }

    public static void ConfigureClientPaths(string appRootPath, string apiBaseAddress)
    {
        AppRootPathProperty.SetValue(null, appRootPath);
        ExternalApiBaseAddressProperty.SetValue(null, apiBaseAddress);
        ProxyApiBaseAddressProperty.SetValue(null, apiBaseAddress);
        ApiService.BaseAddress = apiBaseAddress;
        ApiService.ExternalBaseAddress = apiBaseAddress;
    }

    public static void SetBlazorScriptReferences(params string[] scriptReferences)
        => BlazorScriptReferencesField.SetValue(null, scriptReferences);

    public static IReadOnlyDictionary<string, Type> GetEntityComponentTypes()
        => (IReadOnlyDictionary<string, Type>)(BlazorEntityComponentTypesProperty.GetValue(null)
            ?? throw new InvalidOperationException("Entity component types are unavailable."));
}
