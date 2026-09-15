using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;
using GiftLists.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

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
    public static async Task<(GiftList List, GiftItemId ItemId)> SeedListWithItemAsync(GiftListsFixture fixture, Guid ownerId)
    {
        var now = DateTimeOffset.UtcNow;
        var list = GiftList.Create(
            GiftListId.New(),
            new OwnerId(ownerId),
            new GiftListName("Original Name"),
            new ExpiryDate(now.AddDays(7), now),
            new ShareToken(RandomShareToken()),
            now);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, now);
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
}
