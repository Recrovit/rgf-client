using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Tests.Testing;

internal static class RgfClientTestState
{
    private static readonly Type ConfigurationType = typeof(RgfClientConfiguration);
    private static readonly FieldInfo LoggerFactoryField = typeof(RgfLoggerFactory)
        .GetField("LoggerFactory", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RgfLoggerFactory.LoggerFactory field.");

    public static void Reset()
    {
        SetConfigurationProperty(nameof(RgfClientConfiguration.IsInitialized), false);
        SetConfigurationProperty(nameof(RgfClientConfiguration.AppRootPath), string.Empty);
        SetConfigurationProperty(nameof(RgfClientConfiguration.ExternalApiBaseAddress), string.Empty);
        SetConfigurationProperty(nameof(RgfClientConfiguration.ProxyApiBaseAddress), string.Empty);
        SetConfigurationProperty(nameof(RgfClientConfiguration.ApiAuthMode), RgfApiAuthMode.None);
        RgfClientConfiguration.MinimumRgfCoreVersion = new Version(10, 0, 0);
        RgfClientConfiguration.ClientVersions.Clear();

        ApiService.BaseAddress = string.Empty;
        ApiService.ExternalBaseAddress = string.Empty;

        LoggerFactoryField.SetValue(null, null);
    }

    public static int CountClientVersionHeaders()
        => RgfClientConfiguration.ClientVersions.Count(pair => pair.Key == RgfHeaderKeys.RgfClientVersion);

    private static void SetConfigurationProperty(string propertyName, object? value)
    {
        var property = ConfigurationType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Unable to locate RgfClientConfiguration.{propertyName} property.");

        property.SetValue(null, value);
    }
}
