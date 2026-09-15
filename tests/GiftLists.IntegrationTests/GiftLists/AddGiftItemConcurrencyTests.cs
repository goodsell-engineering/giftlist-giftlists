using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Bus;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// GL-64: two concurrent <c>AddGiftItem</c> messages for the same list, both reading the same
/// persisted state and both appending, must not let a whole-document <c>ReplaceOneAsync</c>
/// silently drop one of them. Entered at the real wire boundary, not by calling
/// <see cref="IGiftListRepository"/> directly (GiftListRepositoryTests's own doc comment explains
/// when that shortcut is taken instead — this is not one of those cases): the bug is specifically
/// about what happens when two of GiftLists' own real Rebus handlers race each other, and Rebus's
/// default <c>MaxParallelism</c> (5, unset by <c>RebusConfigurationExtensions</c> — same default
/// production runs with) is what makes that race possible from a single worker at all.
///
/// The two <c>Send</c> calls below are fired via <c>Task.WhenAll</c> with no await between them,
/// so both messages reach RabbitMQ within the same instant and the GiftLists host's thread pool
/// is free to dequeue and start processing both before either has produced a Mongo write — that
/// is the race this test needs, not merely "two messages sent close together". It is still a real
/// race against real infrastructure, not a forced one (this suite has no seam to force message
/// interleaving deterministically), so a single run is evidence, not proof — see this class's own
/// test author's report for how many repeat runs were used to gain confidence.
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class AddGiftItemConcurrencyTests(GiftListsFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddGiftItem_ShouldPersistBothItems_WhenTwoAddsRaceOnTheSameList()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var first = new AddGiftItem(list.Id.Value, ownerId, firstItemId, "Coffee grinder", null, null);
        var second = new AddGiftItem(list.Id.Value, ownerId, secondItemId, "Board game", null, null);

        // Act — no await between the two Sends: both must be in flight at once for this to be a
        // genuine race rather than two sequential writes (this class's own doc comment).
        await Task.WhenAll(
            fixture.RequesterBus.Send(first),
            fixture.RequesterBus.Send(second));
        var persisted = await PollUntilBothItemsPersistAsync(list.Id, firstItemId, secondItemId, TimeSpan.FromSeconds(20));

        // Assert
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Items.Count);
        var reloadedFirst = Assert.Single(persisted.Items, item => item.Id.Value == firstItemId);
        Assert.Equal("Coffee grinder", reloadedFirst.Name.Value);
        var reloadedSecond = Assert.Single(persisted.Items, item => item.Id.Value == secondItemId);
        Assert.Equal("Board game", reloadedSecond.Name.Value);
    }

    [Fact]
    public async Task AddGiftItem_ShouldPublishGiftItemAddedV1Once_WhenTheSameAddIsDeliveredTwiceConcurrently()
    {
        // Arrange — at-least-once delivery means one AddGiftItem can arrive twice, and GL-20's
        // guard in AddGiftItemInteractor ("does this list already hold this itemId?") is a
        // read-then-check, so two OVERLAPPING deliveries can both pass it. What stops the second
        // one mattering has to be UpdateAsync, and it has to stop it BEFORE the interactor's
        // unconditional PublishAsync — a second GiftItemAddedV1 carries its own, later AddedAt
        // (each delivery stamps its own clock.UtcNow), so the Gateway projection's
        // "current.UpdatedAt == addedAt" exact-redelivery check does not recognise it, applies it
        // as a real write, and advances the item's UpdatedAt. That is the very field the
        // tombstone tie-break uses to reject a stale remove, so a GiftItemRemovedV1 timestamped
        // between the two adds would then be silently discarded (GL-68 review).
        //
        // Storage alone cannot see this: the list holds exactly one item either way. The wire is
        // the only place the difference is visible, which is why this asserts on what was
        // published rather than on what was stored.
        var ownerId = Guid.NewGuid();
        var list = await GiftListSeeding.SeedListAsync(fixture, ownerId);
        var itemId = Guid.NewGuid();
        await using var subscriber = await EventSubscriber<GiftItemAddedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var duplicate = new AddGiftItem(list.Id.Value, ownerId, itemId, "Coffee grinder", null, null);

        // Act — twenty deliveries of one logical add, queued together and then handled together.
        //
        // READ THIS BEFORE TRUSTING A GREEN FROM THIS TEST. Whether the deliveries actually
        // OVERLAP is a property of the host, not of this test, and it could not be forced from
        // here. Measured against code with the regression deliberately reintroduced: run on its
        // own it fails every time, always with exactly 5 published events (Rebus's MaxParallelism
        // — five handlers reading before any of them writes); run inside the full suite it passes
        // every time. The difference is warm-up. On a cold host the first handler pays JIT and
        // Mongo connection costs, which is long enough for the others to read the same state; in
        // a warm suite each delivery retires faster than the workers stagger, so every later one
        // reads the item already committed and stops at GL-20's guard, and the run proves
        // nothing. Stopping the consumer while the queue fills (below) guarantees they are
        // QUEUED together, and raising the worker count guarantees they are PICKED UP together,
        // but neither makes a warm handler slow enough to still be mid-flight when the next
        // starts. Only a seam inside the interactor could do that, and production code does not
        // get a seam for a test's benefit.
        //
        // So this test is evidence, not proof, and a green from it inside the suite is closer to
        // no information than to a pass. The guarantee lives in
        // GiftListRepositoryTests.UpdateAsync_ShouldThrowConcurrencyException_WhenTheSameItemWasAlreadyAddedConcurrently,
        // which forces the same collision with no timing dependency at all. What this one adds,
        // when it does overlap, is the end-to-end evidence that the conflict really does keep the
        // duplicate off the wire rather than merely out of Mongo — which is the half the
        // repository test cannot see.
        using var scope = fixture.CreateGiftListsScope();
        var giftListsWorkers = scope.ServiceProvider.GetRequiredService<IBus>().Advanced.Workers;
        var originalWorkerCount = giftListsWorkers.Count;
        giftListsWorkers.SetNumberOfWorkers(0);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => fixture.RequesterBus.Send(duplicate)));
        }
        finally
        {
            // Restarted with one worker per in-flight delivery rather than the default single
            // one. Stopping the consumer only guarantees the messages are QUEUED together; with
            // Rebus's default of one worker they are still handled one at a time, each reading
            // the previous one's committed write and stopping at GL-20's guard — which is exactly
            // the vacuous pass this is trying to remove. Concurrency is capped independently by
            // MaxParallelism (5, the production default, untouched here), so this changes how
            // many deliveries are picked up at once, not how many may run at once.
            //
            // Restored to the original count at the end of the test even if a Send throws: this
            // is the shared fixture's own host, and leaving it misconfigured would affect every
            // test that runs after this one.
            giftListsWorkers.SetNumberOfWorkers(Math.Max(originalWorkerCount, 5));
        }

        await subscriber.Capture.Completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        // A second publish, if it happens at all, happens within a handler's turnaround of the
        // first; asserting an absence needs a bounded settle window and there is no completion to
        // await for an event that should never come.
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Single(subscriber.Capture.All);
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(list.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        var only = Assert.Single(persisted.Items);
        Assert.Equal(itemId, only.Id.Value);
        giftListsWorkers.SetNumberOfWorkers(originalWorkerCount);
    }

    /// <summary>
    /// Both handlers run asynchronously, so this polls rather than reading once right after
    /// <c>Send</c> returns — that only confirms the messages were accepted onto the queue, not
    /// that either handler has finished.
    /// </summary>
    /// <remarks>
    /// Until GL-68 the loser of the race also had to fail its version-guarded write and wait for
    /// Rebus to redeliver it; it now completes first time, because two adds of different items no
    /// longer conflict. This test cannot see that difference — both items are persisted either
    /// way, which is exactly why
    /// <c>GiftListRepositoryTests.UpdateAsync_ShouldSucceedWithoutConflicting_WhenAConcurrentWriterAddedADifferentItem</c>
    /// exists and asserts on the write's own outcome instead of on the end state. What this test
    /// still pins, and that one does not, is that nothing is lost at the real wire boundary with
    /// real Rebus parallelism behind it.
    /// </remarks>
    private async Task<GiftList?> PollUntilBothItemsPersistAsync(
        GiftListId listId, Guid firstItemId, Guid secondItemId, TimeSpan timeout)
    {
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var current = await repository.FindByIdAsync(listId, CancellationToken.None);
            if (current is not null &&
                current.Items.Any(item => item.Id.Value == firstItemId) &&
                current.Items.Any(item => item.Id.Value == secondItemId))
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return await repository.FindByIdAsync(listId, CancellationToken.None);
    }
}
