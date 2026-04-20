using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Tests.Testing;

internal static class RgfClientTestState
{
    private static readonly FieldInfo LoggerFactoryField = typeof(RgfLoggerFactory)
        .GetField("LoggerFactory", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Unable to locate RgfLoggerFactory.LoggerFactory field.");

    public static void Reset()
    {
        RgfClientConfiguration.IsInitialized = false;
        RgfClientConfiguration.AppRootPath = string.Empty;
        RgfClientConfiguration.ExternalApiBaseAddress = string.Empty;
        RgfClientConfiguration.ProxyApiBaseAddress = string.Empty;
        RgfClientConfiguration.ApiAuthMode = RgfApiAuthMode.None;
        RgfClientConfiguration.MinimumRgfCoreVersion = new Version(10, 0, 0);
        RgfClientConfiguration.ClientVersions.Clear();

        ApiService.BaseAddress = string.Empty;
        ApiService.ExternalBaseAddress = string.Empty;

        LoggerFactoryField.SetValue(null, null);
    }

    public static int CountClientVersionHeaders()
        => RgfClientConfiguration.ClientVersions.Count(pair => pair.Key == RgfHeaderKeys.RgfClientVersion);
}
