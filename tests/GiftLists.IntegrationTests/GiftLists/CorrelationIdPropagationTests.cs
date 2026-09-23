using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.Logging;
using Rebus.Messages;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// GL-45: the middle hop of ARCHITECTURE.md "Cross-cutting concerns" — a correlation id present on
/// an inbound command's Rebus headers (exactly as the Gateway's own
/// <c>CorrelationIdOutgoingStep</c> would have stamped it) reaches this handler's own structured
/// logs, with no per-handler code, AND survives unchanged onto the <c>GiftListCreatedV1</c> this
/// command's handling publishes. GiftLists is the real host under test here (CONVENTIONS.md
/// "Testing"); the Gateway side of this chain — seeding the very first id and stamping it on the
/// Send — is proven separately, against a real Gateway host, by
/// <c>Gateway.IntegrationTests.Platform.CorrelationIdPropagationTests</c>. This test sends the
/// command with an explicit header rather than reproducing that step's own already-tested
/// behaviour here.
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class CorrelationIdPropagationTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateGiftList_ShouldCarryTheInboundCorrelationId_IntoItsLogsAndItsPublishedEvent()
    {
        // Arrange
        var correlationId = $"trace-{Guid.NewGuid():N}";
        await using var subscriber = await EventSubscriber<GiftListCreatedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var listId = Guid.NewGuid();
        var logsBefore = fixture.Logs.Entries.Count;

        // Act — simulates the header Gateway's own CorrelationIdOutgoingStep would have stamped
        // on this Send (see this class's own remarks for why that step is not re-exercised here).
        await fixture.RequesterBus.Send(
            new CreateGiftList(listId, Guid.NewGuid(), "Traced List", DateTimeOffset.UtcNow.AddDays(7)),
            new Dictionary<string, string> { [Headers.CorrelationId] = correlationId });
        await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert — the same id survived onto the published event's own header...
        Assert.Equal(correlationId, subscriber.Capture.LastCorrelationId);

        // ...and at least one log line written while handling it reported that id in the message
        // text itself, not only as a scope property BuildingBlocks.Testing's LogCapture cannot
        // see (GL-117) — CorrelationIdIncomingStep's own guaranteed log line, applied once in
        // BuildingBlocks rather than by this (or any) handler.
        var logged = fixture.Logs.Entries.Skip(logsBefore).ToList();
        Assert.Contains(logged, e => e.Level == LogLevel.Information && e.Message.Contains(correlationId, StringComparison.Ordinal));
    }
}
