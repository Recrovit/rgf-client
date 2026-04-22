using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Recrovit.RecroGridFramework.Client.Services;

public static class RegistrationLoggerResolver
{
    public static ILogger Resolve(IServiceCollection services, ILogger? logger, Type categoryType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(categoryType);

        if (logger != null)
        {
            return logger;
        }

        using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        return loggerFactory?.CreateLogger(categoryType) ?? NullLogger.Instance;
    }
}
