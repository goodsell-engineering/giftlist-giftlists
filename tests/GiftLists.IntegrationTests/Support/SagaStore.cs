using GiftLists.Infrastructure.Platform;
using GiftLists.IntegrationTests.Fixtures;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GiftLists.IntegrationTests.Support;

/// <summary>
/// Reads Rebus's own Mongo collections — the saga data and the deferred timeouts
/// <see cref="GiftListsRebusConfiguration"/> points at — so a test can assert that the expiry
/// saga's state genuinely lives in Mongo (ARCHITECTURE.md "Sagas: list expiry": "Mongo-backed")
/// and that it ends when it should, rather than inferring both from which events arrived. Read
/// as raw <see cref="BsonDocument"/>s on purpose: the saga data type is internal to
/// Infrastructure, and what is being checked is what is on disk, not what a class map says.
/// </summary>
internal static class SagaStore
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static Task<BsonDocument?> FindSagaAsync(GiftListsFixture fixture, Guid listId) =>
        fixture.Database
            .GetCollection<BsonDocument>(GiftListsRebusConfiguration.SagasCollectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("ListId", listId))
            .FirstOrDefaultAsync()!;

    public static Task<long> CountTimeoutsAsync(GiftListsFixture fixture) =>
        fixture.Database
            .GetCollection<BsonDocument>(GiftListsRebusConfiguration.TimeoutsCollectionName)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty);

    /// <summary>
    /// Polls until the timeout collection holds exactly <paramref name="expected"/> documents, or
    /// <paramref name="timeout"/> elapses. Polled rather than read once because the timeout is
    /// stored by a second hop — the saga's DeferLocal sends it back to the bus's own queue, and
    /// only there does Rebus write it to Mongo — and removed by a third, after it is dispatched.
    /// </summary>
    public static async Task WaitForTimeoutCountAsync(GiftListsFixture fixture, long expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        long count;
        do
        {
            count = await CountTimeoutsAsync(fixture);
            if (count == expected)
            {
                return;
            }

            await Task.Delay(PollInterval);
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new TimeoutException($"Expected {expected} stored timeout(s) but found {count} after {timeout}.");
    }

    /// <summary>Polls until a saga for the list exists and <paramref name="condition"/> holds of it, or <paramref name="timeout"/> elapses.</summary>
    public static async Task<BsonDocument> WaitForSagaAsync(
        GiftListsFixture fixture, Guid listId, Func<BsonDocument, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var saga = await FindSagaAsync(fixture, listId);
            if (saga is not null && condition(saga))
            {
                return saga;
            }

            await Task.Delay(PollInterval);
        }

        throw new TimeoutException($"No saga for list {listId} satisfied the condition within {timeout}.");
    }

    /// <summary>Polls until no saga for the list exists, or <paramref name="timeout"/> elapses.</summary>
    public static async Task WaitForSagaToEndAsync(GiftListsFixture fixture, Guid listId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await FindSagaAsync(fixture, listId) is null)
            {
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new TimeoutException($"The saga for list {listId} had not ended within {timeout}.");
    }

    /// <summary>The expiry the saga currently holds, as the UTC instant it was stored as.</summary>
    public static DateTimeOffset ExpiresAtOf(BsonDocument saga) =>
        new(saga["ExpiresAtUtc"].ToUniversalTime(), TimeSpan.Zero);
}
