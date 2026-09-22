using GiftLists.Contracts.GiftLists;
using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.Common;
using GiftLists.Infrastructure.Platform;
using GiftLists.IntegrationTests.Fixtures;
using GiftLists.IntegrationTests.Support;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GiftLists.IntegrationTests.GiftLists;

/// <summary>
/// The expiry saga of ARCHITECTURE.md "Sagas: list expiry", driven end to end: a command over
/// the real broker, the saga started by this service's own published event, its state and its
/// deferred timeout in real Mongo, and the timeout coming due on a real clock. Nothing here
/// asserts that <c>Defer</c> was called — a list is given an expiry a few seconds out and the
/// test waits for <see cref="GiftListExpiredV1"/> to arrive off a real subscription, or not.
/// Every wait is bounded well above Rebus's one-second due-timeout poll.
/// </summary>
[Collection(GiftListsCollection.Name)]
public sealed class GiftListExpirySagaTests(GiftListsFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan SagaWait = TimeSpan.FromSeconds(15);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExpirySagaCollection_ShouldCarryTheUniqueCorrelationIndex_AfterStartup()
    {
        // Arrange — ResetAsync has just dropped the database and re-run EnsureIndexesAsync, which
        // is exactly the state every other test in this suite starts from. That is the point:
        // Rebus.MongoDb's own lazy index creation would NOT have survived that drop (it happens
        // once per process, on the first saga insert), so before GL-41's review fold every test
        // after the first ran against an unindexed saga collection.
        var indexes = await fixture.Database
            .GetCollection<BsonDocument>(GiftListsRebusConfiguration.SagasCollectionName)
            .Indexes.ListAsync();
        var declared = await indexes.ToListAsync();

        // Act
        var correlationIndex = declared.SingleOrDefault(
            index => index["name"].AsString == "listId_unique");

        // Assert — present, unique, and over the correlation element Rebus itself filters saga
        // lookups on. Uniqueness is the one-saga-per-list guarantee: two rows would mean two
        // timeouts and two GiftListExpiredV1s for one list.
        Assert.NotNull(correlationIndex);
        Assert.True(correlationIndex["unique"].AsBoolean);
        Assert.Equal(
            1,
            correlationIndex["key"][GiftListsRebusConfiguration.SagaCorrelationElementName].AsInt32);
    }

    [Fact]
    public async Task ExpirySaga_ShouldHoldItsStateAndTimeoutInMongo_OnceTheListIsCreated()
    {
        // Arrange — an expiry far enough out that nothing fires during the test.
        var listId = Guid.NewGuid();
        var expiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddDays(7));

        // Act
        await fixture.RequesterBus.Send(new CreateGiftList(listId, Guid.NewGuid(), "Birthday Wishlist", expiresAt));
        var saga = await SagaStore.WaitForSagaAsync(fixture, listId, _ => true, SagaWait);

        // Assert
        Assert.Equal(expiresAt, SagaStore.ExpiresAtOf(saga));
        await SagaStore.WaitForTimeoutCountAsync(fixture, expected: 1, SagaWait);
    }

    [Fact]
    public async Task ExpirySaga_ShouldPublishGiftListExpiredV1AndEnd_WhenTheExpiryElapses()
    {
        // Arrange — a real, short expiry on the real clock.
        var listId = Guid.NewGuid();
        var expiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddSeconds(3));
        await using var subscriber = await EventSubscriber<GiftListExpiredV1>.StartAsync(fixture.RabbitMqConnectionString);

        // Act
        await fixture.RequesterBus.Send(new CreateGiftList(listId, Guid.NewGuid(), "Birthday Wishlist", expiresAt));
        var expired = await subscriber.Capture.Completion.Task.WaitAsync(SagaWait);
        var receivedAt = DateTimeOffset.UtcNow;

        // Assert
        Assert.Equal(listId, expired.ListId);
        Assert.Equal(expiresAt, expired.ExpiresAt);
        // Not early: the timeout came due on the clock, it was not dispatched on arrival.
        Assert.True(receivedAt >= expiresAt, $"GiftListExpiredV1 arrived at {receivedAt:O}, before the expiry {expiresAt:O}.");
        await SagaStore.WaitForSagaToEndAsync(fixture, listId, SagaWait);
        await SagaStore.WaitForTimeoutCountAsync(fixture, expected: 0, SagaWait);
    }

    [Fact]
    public async Task ExpirySaga_ShouldEndWithoutPublishing_WhenTheListIsDeletedBeforeItExpires()
    {
        // Arrange — the saga is running and holds a timeout a few seconds out.
        var ownerId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var expiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddSeconds(4));
        await using var subscriber = await EventSubscriber<GiftListExpiredV1>.StartAsync(fixture.RabbitMqConnectionString);
        await fixture.RequesterBus.Send(new CreateGiftList(listId, ownerId, "Birthday Wishlist", expiresAt));
        await SagaStore.WaitForSagaAsync(fixture, listId, _ => true, SagaWait);

        // Act
        await fixture.RequesterBus.Send(new DeleteGiftList(listId, ownerId));
        await SagaStore.WaitForSagaToEndAsync(fixture, listId, SagaWait);
        // Let the (now orphaned) timeout come due and be dropped, then look for what it did NOT do.
        var waitPastExpiry = expiresAt - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(4);
        var exception = await Record.ExceptionAsync(() => subscriber.Capture.Completion.Task.WaitAsync(waitPastExpiry));

        // Assert
        Assert.IsType<TimeoutException>(exception);
        Assert.Empty(subscriber.Capture.All);
    }

    [Fact]
    public async Task ExpirySaga_ShouldFireAtTheNewExpiryOnly_WhenTheExpiryIsChanged()
    {
        // Arrange — created expiring in 3s, then moved to 8s before the first timeout comes due.
        var ownerId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var originalExpiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddSeconds(3));
        var newExpiresAt = Timestamps.ToStoredPrecision(DateTimeOffset.UtcNow.AddSeconds(8));
        await using var subscriber = await EventSubscriber<GiftListExpiredV1>.StartAsync(fixture.RabbitMqConnectionString);
        await fixture.RequesterBus.Send(new CreateGiftList(listId, ownerId, "Birthday Wishlist", originalExpiresAt));
        await SagaStore.WaitForSagaAsync(fixture, listId, _ => true, SagaWait);

        // Act
        await fixture.RequesterBus.Send(new ChangeGiftListExpiry(listId, ownerId, newExpiresAt));
        var rescheduled = await SagaStore.WaitForSagaAsync(
            fixture, listId, s => SagaStore.ExpiresAtOf(s) == newExpiresAt, SagaWait);
        var expired = await subscriber.Capture.Completion.Task.WaitAsync(SagaWait);
        var receivedAt = DateTimeOffset.UtcNow;

        // Assert
        Assert.Equal(newExpiresAt, SagaStore.ExpiresAtOf(rescheduled));
        Assert.Equal(newExpiresAt, expired.ExpiresAt);
        // The original timeout still arrived (Rebus cannot cancel it) and was dropped as
        // superseded: nothing fired before the new expiry, and nothing fired twice.
        Assert.True(receivedAt >= newExpiresAt, $"GiftListExpiredV1 arrived at {receivedAt:O}, before the new expiry {newExpiresAt:O}.");
        await SagaStore.WaitForSagaToEndAsync(fixture, listId, SagaWait);
        Assert.Single(subscriber.Capture.All);
    }
}
