using GiftLists.Application.Common;
using GiftLists.Domain.Common;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// <see cref="IDomainEventPublisher"/>'s one hard guarantee: it never throws. Resolved through the
/// composition root rather than constructed directly, following Identity's
/// <c>UserEventPublishingTests</c>, so no extra internals access is needed.
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class GiftListEventPublisherTests(GiftListsFixture fixture)
{
    /// <summary>A domain event the mapper has no arm for. Declared in the TEST assembly, so it is
    /// invisible to the exhaustiveness guard that reflects over GiftLists.Domain.</summary>
    private sealed record UnmappedDomainEvent : IDomainEvent;

    [Fact]
    public async Task PublishAsync_ShouldNotThrow_WhenADomainEventHasNoMapping()
    {
        // Arrange — GiftListEventPublisher swallows and logs Critical rather than rethrowing,
        // because by the time it runs the Mongo write has already succeeded and letting the
        // exception reach Rebus would retry an applied write (ARCHITECTURE.md "Event publishing: synchronous"). That contract
        // was defeated for exactly one input: the mapper's own default-arm throw. The catch block
        // called ToIntegrationEvent again on the failed event to name it in the log, which threw a
        // second time, escaped PublishAsync, and produced NO log at all — the unmapped-event case
        // being the one GL-21's exhaustiveness guard exists to catch.
        //
        // Asserting "does not throw" rather than inspecting the log because not throwing IS the
        // contract; the log is how it reports, and the reporting is worthless if the method dies.
        using var scope = fixture.CreateGiftListsScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

        var before = fixture.Logs.Entries.Count;

        // Act
        var exception = await Record.ExceptionAsync(
            () => publisher.PublishAsync([new UnmappedDomainEvent()], CancellationToken.None));

        // Assert
        Assert.Null(exception);

        // ...and that it actually REPORTED. Asserting only "did not throw" left this green if the
        // LogCritical call were deleted entirely — which is precisely the state the bug produced,
        // so the test could not fail for the reason it exists (Batch 12 review). The publisher's
        // own remarks justify swallowing *because* it logs loudly, so the Critical entry is the
        // contract, not the reporting mechanism for it.
        var written = fixture.Logs.Entries.Skip(before).ToList();
        var critical = Assert.Single(written, e => e.Level == LogLevel.Critical);
        Assert.Contains(nameof(UnmappedDomainEvent), critical.Message, StringComparison.Ordinal);
    }
}
