using GiftLists.Application.Common;

namespace GiftLists.UnitTests.Support;

/// <summary>Hand-written fake (CONVENTIONS.md "Testing" — preferred over a mock for a port used across many tests): settable, deterministic "now" for interactor/value-object tests.</summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; set; }
}
