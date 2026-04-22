using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Configuration;

public sealed class RgfBlazorConfigurationExtensionTests
{
    [Fact]
    public void AddRgfBlazorWithoutAuthServices_WithoutLogger_DoesNotThrow()
    {
        var services = new ServiceCollection();

        services.AddRgfBlazorWithoutAuthServices(CreateConfiguration());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRgfApiService));
    }

    [Fact]
    public void AddRgfBlazorWithoutAuthServices_UsesServiceCollectionLoggerWhenLoggerIsNotProvided()
    {
        var services = new ServiceCollection();
        var loggerProvider = new ListLoggerProvider();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        services.AddRgfBlazorWithoutAuthServices(CreateConfiguration());

        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("AddRgfServices:", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("without built-in authentication handling", StringComparison.Ordinal));
    }

    [Fact]
    public void AddRgfBlazorWithoutAuthServices_PrefersExplicitLoggerOverResolvedLogger()
    {
        var services = new ServiceCollection();
        var loggerProvider = new ListLoggerProvider();
        var explicitLogger = new ListLogger();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        services.AddRgfBlazorWithoutAuthServices(CreateConfiguration(), explicitLogger);

        Assert.NotEmpty(explicitLogger.Entries);
        Assert.Empty(loggerProvider.Entries);
    }

    [Fact]
    public void AddRgfBlazorWithoutAuthServices_MissingBaseAddress_LogsCriticalMessageBeforeThrowing()
    {
        var services = new ServiceCollection();
        var loggerProvider = new ListLoggerProvider();

        services.AddLogging(builder => builder.AddProvider(loggerProvider));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddRgfBlazorWithoutAuthServices(CreateConfiguration(baseAddress: string.Empty)));

        Assert.Contains("BaseAddress", exception.Message, StringComparison.Ordinal);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.Level == LogLevel.Critical &&
            entry.Message.Contains("BaseAddress", StringComparison.Ordinal));
    }

    private static IConfiguration CreateConfiguration(string? baseAddress = "https://api.example.test")
    {
        var values = new Dictionary<string, string?>
        {
            ["Recrovit:RecroGridFramework:API:BaseAddress"] = baseAddress,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
