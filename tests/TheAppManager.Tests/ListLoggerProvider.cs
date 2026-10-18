using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TheAppManager.Tests;

/// <summary>Records every log written through it: level, category and formatted message.</summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(LogLevel Level, string Category, string Message)> Records { get; } = new();

    public ILogger CreateLogger(string categoryName) => new ListLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class ListLogger(ListLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => provider.Records.Enqueue((logLevel, category, formatter(state, exception)));
    }
}
