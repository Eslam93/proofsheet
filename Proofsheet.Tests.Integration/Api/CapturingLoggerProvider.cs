using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Proofsheet.Tests.Integration.Api;

/// <summary>Keeps every log entry written through the host's logger factory so tests can read what the server logged.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string categoryName, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new CapturedLogEntry(categoryName, logLevel, formatter(state, exception), exception));
    }
}

internal sealed record CapturedLogEntry(string Category, LogLevel Level, string Message, Exception? Exception);
