using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GiftLists.IntegrationTests.Support;

/// <summary>
/// Captures log entries written by the GiftLists host, so a test can assert that something was
/// LOGGED rather than only that it did not throw.
/// </summary>
/// <remarks>
/// Added for <c>GiftListEventPublisherTests</c>. That test originally asserted only that
/// <c>PublishAsync</c> does not throw, which stayed green if the <c>LogCritical</c> call were
/// deleted outright — the exact state the bug produced (Batch 12 review). The publisher's own
/// remarks justify swallowing a publish failure *because* it logs loudly, so the log IS the
/// contract, not merely how the contract reports itself.
/// </remarks>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
