using Microsoft.Extensions.Logging;

namespace GiftLists.IntegrationTests.Support;

/// <summary>
/// Waits for the Warning the <c>Logging</c> decorator writes when a use case fails. The handler
/// discards the Result (GL-72), so that log line is how a test observes "result &lt;code&gt;".
/// <see cref="LogCapture"/> is not cleared between tests, so callers snapshot
/// <c>fixture.Logs.Entries.Count</c> before the Act and pass it as <c>since</c>.
/// </summary>
internal static class LogAssertions
{
    public static async Task WaitForWarningAsync(LogCapture logs, int since, string expectedMessage, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Contains(logs, since, expectedMessage))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var seen = string.Join(" | ", logs.Entries.Skip(since).Where(e => e.Level >= LogLevel.Warning).Select(e => e.Message));
        throw new Xunit.Sdk.XunitException($"No Warning containing '{expectedMessage}' was logged within {timeout}. Warnings since: [{seen}]");
    }

    private static bool Contains(LogCapture logs, int since, string expectedMessage) =>
        logs.Entries.Skip(since).Any(e => e.Level == LogLevel.Warning && e.Message.Contains(expectedMessage, StringComparison.Ordinal));
}
