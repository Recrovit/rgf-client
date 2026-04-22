using Microsoft.Extensions.Logging;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Testing;

internal sealed class ListLoggerProvider : ILoggerProvider
{
    public List<ListLogger.LogEntry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(ListLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            provider.Entries.Add(new(logLevel, eventId, $"{categoryName}: {formatter(state, exception)}", exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
