using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace GiftLists.IntegrationTests.Support;

/// <summary>
/// Seeds a list directly through the real <see cref="IGiftListRepository"/> — real Mongo, same
/// as every other write in this suite — rather than through <c>CreateGiftList</c>'s own Rebus
/// handler, so a Rename/Delete/AddItem/RemoveItem test's "Arrange" step does not itself depend on
/// the Create use case working (CONVENTIONS.md "Testing"'s AAA split: setup that isn't the thing under
/// test shouldn't share a failure mode with it).
/// </summary>
internal static class GiftListSeeding
{
    public static async Task<GiftList> SeedListAsync(
        GiftListsFixture fixture, Guid ownerId, DateTimeOffset? expiresAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(),
            new OwnerId(ownerId),
            new GiftListName("Original Name"),
            new ExpiryDate(expiresAt ?? now.AddDays(7), now),
            new ShareToken(RandomShareToken()),
            now);

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var saved = await repository.AddAsync(list, CancellationToken.None);
        if (saved.IsFailure)
        {
            throw new InvalidOperationException($"Failed to seed a gift list: {saved.Error.Code}.");
        }

        return list;
    }

    /// <summary>Seeds a list already carrying one item, for RemoveGiftItem's happy path.</summary>
    public static Task<(GiftList List, GiftItemId ItemId)> SeedListWithItemAsync(
        GiftListsFixture fixture, Guid ownerId, string? description = null, int priority = 3) =>
        SeedListWithItemAsync(fixture, ownerId, description, expiresAt: null, priority);

    /// <summary>
    /// The same as above, but able to seed a list that has ALREADY expired — needed for
    /// TC-T2-09's "no edit on an expired list" case. <see cref="ExpiryDate"/>'s validating
    /// constructor refuses a past date (this type's own doc comment), so, like
    /// <c>GiftListRepositoryTests.FindByIdAsync_ShouldRehydrateAnAlreadyExpiredList_WithoutThrowing</c>,
    /// this goes through <see cref="GiftList.Rehydrate"/> instead of <see cref="GiftList.Create"/>.
    /// </summary>
    public static async Task<(GiftList List, GiftItemId ItemId)> SeedListWithItemAsync(
        GiftListsFixture fixture, Guid ownerId, string? description, DateTimeOffset? expiresAt, int priority = 3)
    {
        var now = DateTimeOffset.UtcNow;
        GiftList list;
        if (expiresAt is { } pastExpiry && pastExpiry <= now)
        {
            list = GiftList.Rehydrate(
                GiftListId.New(),
                new OwnerId(ownerId),
                new GiftListName("Original Name"),
                ExpiryDate.Rehydrate(pastExpiry),
                new ShareToken(RandomShareToken()),
                now.AddDays(-30),
                version: 0,
                []);
        }
        else
        {
            list = GiftList.Create(
                GiftListId.New(),
                new OwnerId(ownerId),
                new GiftListName("Original Name"),
                new ExpiryDate(expiresAt ?? now.AddDays(7), now),
                new ShareToken(RandomShareToken()),
                now);
        }

        var itemId = GiftItemId.New();
        list.AddItem(
            itemId,
            new GiftItemName("Coffee grinder"),
            string.IsNullOrWhiteSpace(description) ? null : new GiftItemDescription(description),
            null,
            new GiftItemPriority(priority),
            expiresAt is { } already && already <= now ? now.AddDays(-30) : now);
        list.ClearDomainEvents();

        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var saved = await repository.AddAsync(list, CancellationToken.None);
        if (saved.IsFailure)
        {
            throw new InvalidOperationException($"Failed to seed a gift list: {saved.Error.Code}.");
        }

        return (list, itemId);
    }

    public static string RandomShareToken() => Guid.NewGuid().ToString("N")[..ShareToken.Length];

    /// <summary>Adds another item to an already-seeded list, through the real repository; returns its id.</summary>
    public static async Task<GiftItemId> AddItemAsync(
        GiftListsFixture fixture, GiftListId listId, int priority = 3)
    {
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var loaded = await repository.FindByIdAsync(listId, CancellationToken.None)
            ?? throw new InvalidOperationException("Seeded list not found.");
        var itemId = GiftItemId.New();
        loaded.AddItem(itemId, new GiftItemName("Board game"), null, null, new GiftItemPriority(priority), DateTimeOffset.UtcNow);
        await repository.UpdateAsync(loaded, CancellationToken.None);
        return itemId;
    }

    /// <summary>Removes an item through the real repository, so a "removed item" test's Arrange is deterministic.</summary>
    public static async Task RemoveItemAsync(GiftListsFixture fixture, GiftListId listId, GiftItemId itemId)
    {
        using var scope = fixture.CreateGiftListsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGiftListRepository>();
        var loaded = await repository.FindByIdAsync(listId, CancellationToken.None)
            ?? throw new InvalidOperationException("Seeded list not found.");
        loaded.RemoveItem(itemId, DateTimeOffset.UtcNow);
        await repository.UpdateAsync(loaded, CancellationToken.None);
    }

    /// <summary>
    /// Strips the <c>priority</c> field from every item of a stored list, leaving the raw shape a
    /// pre-GL-146 document had. Raw on purpose: the document type cannot express "absent", and
    /// hand-writing the old shape would drift from what Mongo really holds.
    /// </summary>
    public static async Task StripPriorityFromStoredItemsAsync(GiftListsFixture fixture, GiftListId listId)
    {
        var collection = fixture.Database.GetCollection<MongoDB.Bson.BsonDocument>(GiftListsFixture.GiftListsCollectionName);
        var result = await collection.UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
                "_id", new MongoDB.Bson.BsonBinaryData(listId.Value, MongoDB.Bson.GuidRepresentation.Standard)),
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Unset("items.$[].priority"));
        if (result.ModifiedCount != 1)
        {
            throw new InvalidOperationException("Could not strip priority from the seeded document.");
        }
    }

    /// <summary>The raw stored document of a list, as Mongo holds it.</summary>
    public static async Task<MongoDB.Bson.BsonDocument> GetRawDocumentAsync(GiftListsFixture fixture, GiftListId listId)
    {
        var collection = fixture.Database.GetCollection<MongoDB.Bson.BsonDocument>(GiftListsFixture.GiftListsCollectionName);
        return await collection.Find(
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq(
                    "_id", new MongoDB.Bson.BsonBinaryData(listId.Value, MongoDB.Bson.GuidRepresentation.Standard)))
            .SingleAsync();
    }
}
