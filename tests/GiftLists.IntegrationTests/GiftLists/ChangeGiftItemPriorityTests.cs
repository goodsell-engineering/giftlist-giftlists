using GiftLists.Application.GiftLists;
using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// GL-146. Every "publishes nothing" test sends a follow-up whose event must arrive, so a dead
/// consumer cannot pass as "nothing was published". "Result code" is observed through the Warning
/// the <c>Logging</c> decorator writes, because the handler discards the Result (GL-72); the log
/// entry count is snapshotted before the Act because <see cref="LogCapture"/> is not cleared
/// between tests.
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class ChangeGiftItemPriorityTests(GiftListsFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<GiftList> LoadAsync(GiftListId listId)
    {
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var persisted = await repository.FindByIdAsync(listId, CancellationToken.None);
        Assert.NotNull(persisted);
        return persisted;
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldStoreThePriority_AndPublishGiftItemPriorityChangedV1()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act — T0 is floored to the millisecond, because ChangedAt is stored at millisecond precision.
        var now = DateTimeOffset.UtcNow;
        var t0 = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 5));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        var t1 = DateTimeOffset.UtcNow;

        // Assert
        Assert.Single(subscriber.Capture.All);
        Assert.Equal(list.Id.Value, published.ListId);
        Assert.Equal(itemId.Value, published.ItemId);
        Assert.Equal(5, published.Priority);
        Assert.Equal(0, published.ChangedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.InRange(published.ChangedAt, t0, t1);
        Assert.Equal(5, Assert.Single((await LoadAsync(list.Id)).Items).Priority.Value);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldPublishNothingAndKeepTheStoredVersion_WhenThePriorityIsUnchanged()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId, priority: 4);
        var versionBefore = (await LoadAsync(list.Id)).Version;
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 4));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var versionAfterNoOp = (await LoadAsync(list.Id)).Version;
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 2));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);

        // Assert
        Assert.Equal(versionBefore, versionAfterNoOp);
        Assert.Equal(2, followUp.Priority);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldLeaveTheItemUnchangedAndPublishNothing_WhenThePriorityIsOutside1To5()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        const string expected = "ChangeGiftItemPriorityRequest failed: giftlist.item_priority_invalid";

        // Act + Assert, per value
        foreach (var invalid in new[] { 0, 6 })
        {
            var before = fixture.Logs.Entries.Count;
            await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, invalid));
            await LogAssertions.WaitForWarningAsync(fixture.Logs, before, expected, Wait);
            Assert.Equal(3, Assert.Single((await LoadAsync(list.Id)).Items).Priority.Value);
        }

        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 1));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Equal(1, followUp.Priority);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldLeaveTheItemUnchangedAndPublishNothing_WhenTheRequesterIsNotTheOwner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var logsBefore = fixture.Logs.Entries.Count;

        // Act — step 1
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, Guid.NewGuid(), itemId.Value, 5));
        await LogAssertions.WaitForWarningAsync(
            fixture.Logs, logsBefore, "ChangeGiftItemPriorityRequest failed: giftlist.forbidden", Wait);
        var storedAfterRejection = Assert.Single((await LoadAsync(list.Id)).Items).Priority.Value;

        // Act — step 2, proves the consumer is alive
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 4));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(3, storedAfterRejection);
        Assert.Equal(4, followUp.Priority);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldPublishNothing_WhenTheListDoesNotExist()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var missingListId = Guid.NewGuid();
        var (listM, itemJ) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var logsBefore = fixture.Logs.Entries.Count;

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(missingListId, ownerId, Guid.NewGuid(), 5));
        await LogAssertions.WaitForWarningAsync(
            fixture.Logs, logsBefore, "ChangeGiftItemPriorityRequest failed: giftlist.not_found", Wait);
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(listM.Id.Value, ownerId, itemJ.Value, 4));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(listM.Id.Value, followUp.ListId);
        Assert.DoesNotContain(subscriber.Capture.All, e => e.ListId == missingListId);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldPublishNothing_WhenTheItemWasRemoved()
    {
        // Arrange — list with items I and J; I is removed through the real repository.
        var ownerId = Guid.NewGuid();
        var (list, itemI) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        var itemJ = await GiftListSeeding.AddItemAsync(fixture, list.Id);
        await GiftListSeeding.RemoveItemAsync(fixture, list.Id, itemI);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var logsBefore = fixture.Logs.Entries.Count;

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemI.Value, 5));
        await LogAssertions.WaitForWarningAsync(
            fixture.Logs, logsBefore, "ChangeGiftItemPriorityRequest failed: giftlist.item_not_found", Wait);
        var itemsAfterRejection = (await LoadAsync(list.Id)).Items;
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemJ.Value, 4));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        Assert.DoesNotContain(itemsAfterRejection, i => i.Id == itemI);
        Assert.Equal(itemJ.Value, followUp.ItemId);
        Assert.DoesNotContain(subscriber.Capture.All, e => e.ItemId == itemI.Value);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldLeaveTheItemUnchangedAndPublishNothing_WhenTheListHasExpired()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (expired, itemI) = await GiftListSeeding.SeedListWithItemAsync(
            fixture, ownerId, description: null, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        var (live, itemJ) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var logsBefore = fixture.Logs.Entries.Count;

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(expired.Id.Value, ownerId, itemI.Value, 5));
        await LogAssertions.WaitForWarningAsync(
            fixture.Logs, logsBefore, "ChangeGiftItemPriorityRequest failed: giftlist.expired", Wait);
        var storedAfterRejection = Assert.Single((await LoadAsync(expired.Id)).Items).Priority.Value;
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(live.Id.Value, ownerId, itemJ.Value, 4));
        var followUp = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(3, storedAfterRejection);
        Assert.Equal(live.Id.Value, followUp.ListId);
        Assert.DoesNotContain(subscriber.Capture.All, e => e.ListId == expired.Id.Value);
        Assert.Single(subscriber.Capture.All);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldTreatAStoredItemWithNoPriorityAs3()
    {
        // Arrange — a raw document with no priority field, as stored before GL-146.
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await GiftListSeeding.StripPriorityFromStoredItemsAsync(fixture, list.Id);
        var rawBefore = await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id);
        Assert.False(rawBefore["items"].AsBsonArray.Single().AsBsonDocument.Contains("priority"));
        var versionBefore = rawBefore["version"].ToInt64();
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act — step 1: 3 equals the resolved default, so a no-op.
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 3));
        await Task.Delay(TimeSpan.FromSeconds(3));
        var rawAfterNoOp = await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id);

        // Act — step 2
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 4));
        var published = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var rawAfter = await GiftListSeeding.GetRawDocumentAsync(fixture, list.Id);

        // Assert
        Assert.Equal(versionBefore, rawAfterNoOp["version"].ToInt64());
        Assert.False(rawAfterNoOp["items"].AsBsonArray.Single().AsBsonDocument.Contains("priority"));
        Assert.Equal(4, published.Priority);
        Assert.Single(subscriber.Capture.All);
        Assert.Equal(4, rawAfter["items"].AsBsonArray.Single().AsBsonDocument["priority"].AsInt32);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldPublishOnce_WhenTheSameCommandIsDeliveredTwice()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        var (otherList, otherItemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        var command = new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 5);

        // Act
        await fixture.RequesterBus.Send(command);
        var published = await subscriber.Capture.Completion.Task.WaitAsync(Wait);
        await fixture.RequesterBus.Send(command);
        await Task.Delay(TimeSpan.FromSeconds(5));
        await fixture.RequesterBus.Send(new ChangeGiftItemPriority(otherList.Id.Value, ownerId, otherItemId.Value, 1));
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert — one event for the list, plus the follow-up proving the consumer is alive.
        Assert.Equal(5, published.Priority);
        Assert.Equal(2, subscriber.Capture.All.Count);
        Assert.Single(subscriber.Capture.All, e => e.ListId == list.Id.Value);
        Assert.Equal(5, Assert.Single((await LoadAsync(list.Id)).Items).Priority.Value);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldLandAlongsideADescriptionChange_WhenBothRaceOnTheSameItem()
    {
        // Arrange — the deterministic step 2 of TC-GL-146-14 is
        // GiftListRepositoryTests.UpdateAsync_ShouldThrowConcurrencyException_WhenAnotherWriterChangedTheDescriptionBeforeThePriorityChangeSaved.
        // Both writes are version-guarded, so one of a real race loses, throws
        // GiftListConcurrencyException, and lands only because Rebus retries it.
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var prioritySubscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);
        await using var descriptionSubscriber = await EventSubscriber<GiftItemDescriptionChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act — no await between the two Sends.
        await Task.WhenAll(
            fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 5)),
            fixture.RequesterBus.Send(new ChangeGiftItemDescription(list.Id.Value, ownerId, itemId.Value, "Size M")));
        var priorityEvent = await prioritySubscriber.Capture.Completion.Task.WaitAsync(Wait);
        var descriptionEvent = await descriptionSubscriber.Capture.Completion.Task.WaitAsync(Wait);
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(5, priorityEvent.Priority);
        Assert.Equal("Size M", descriptionEvent.Description);
        Assert.Single(prioritySubscriber.Capture.All);
        Assert.Single(descriptionSubscriber.Capture.All);
        var item = Assert.Single((await LoadAsync(list.Id)).Items);
        Assert.Equal(5, item.Priority.Value);
        Assert.Equal("Size M", item.Description?.Value);
    }

    [Fact]
    public async Task ChangeGiftItemPriority_ShouldStoreTheValueWithTheLaterChangedAt_WhenTwoDifferentChangesRace()
    {
        // Arrange — evidence, not proof (see ChangeGiftItemDescriptionTests for the reasoning); the
        // deterministic guarantee is the repository concurrency tests.
        var ownerId = Guid.NewGuid();
        var (list, itemId) = await GiftListSeeding.SeedListWithItemAsync(fixture, ownerId);
        await using var subscriber = await EventSubscriber<GiftItemPriorityChangedV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await Task.WhenAll(
            fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 1)),
            fixture.RequesterBus.Send(new ChangeGiftItemPriority(list.Id.Value, ownerId, itemId.Value, 5)));
        var captured = await PollUntilTwoEventsAreCapturedAsync(subscriber, list.Id.Value, TimeSpan.FromSeconds(20));
        var persisted = await LoadAsync(list.Id);

        // Assert
        Assert.Equal(2, captured.Count);
        var winner = captured[0].ChangedAt > captured[1].ChangedAt ? captured[0] : captured[1];
        var loser = captured[0].ChangedAt > captured[1].ChangedAt ? captured[1] : captured[0];
        if (winner.ChangedAt == loser.ChangedAt)
        {
            Assert.Fail(
                $"Both captured ChangedAt values were equal ({winner.ChangedAt:O}) — the accepted " +
                "same-millisecond drift (GL-135 design, 'Failure modes'). Inconclusive for " +
                "'later ChangedAt wins'; rerun.");
        }

        Assert.Equal(winner.Priority, Assert.Single(persisted.Items).Priority.Value);
    }

    private static async Task<IReadOnlyList<GiftItemPriorityChangedV1>> PollUntilTwoEventsAreCapturedAsync(
        EventSubscriber<GiftItemPriorityChangedV1> subscriber, Guid listId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var forThisList = subscriber.Capture.All.Where(e => e.ListId == listId).ToArray();
            if (forThisList.Length >= 2)
            {
                return forThisList;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return subscriber.Capture.All.Where(e => e.ListId == listId).ToArray();
    }
}
