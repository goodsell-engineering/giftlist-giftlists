using GiftLists.Domain.GiftLists;

namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>Source + "To" + target (CONVENTIONS.md "Naming").</summary>
internal static class GiftListToDocumentMapper
{
    public static GiftListDocument ToDocument(GiftList list) => new()
    {
        Id = list.Id.Value,
        OwnerId = list.OwnerId.Value,
        Name = list.Name.Value,
        // GiftListDocument's own doc comment explains why these are DateTime, not the
        // aggregate's DateTimeOffset — both are always UTC already, so .UtcDateTime is lossless.
        ExpiresAt = list.Expiry.Value.UtcDateTime,
        ShareToken = list.ShareToken.Value,
        CreatedAt = list.CreatedAt.UtcDateTime,
        Version = list.Version,
        Items = list.Items.Select(item => new GiftItemDocument
        {
            ItemId = item.Id.Value,
            Name = item.Name.Value,
            Description = item.Description?.Value,
            Url = item.Url?.Value,
        }).ToList(),
    };

    public static GiftList ToAggregate(GiftListDocument document) => GiftList.Rehydrate(
        new GiftListId(document.Id),
        new OwnerId(document.OwnerId),
        new GiftListName(document.Name),
        // ExpiryDate.Rehydrate, not the validating constructor: a list loaded back out of
        // storage may legitimately already be expired (ExpiryDate's own doc comment).
        ExpiryDate.Rehydrate(new DateTimeOffset(document.ExpiresAt, TimeSpan.Zero)),
        new ShareToken(document.ShareToken),
        new DateTimeOffset(document.CreatedAt, TimeSpan.Zero),
        document.Version,
        document.Items.Select(item => GiftItem.Create(
            new GiftItemId(item.ItemId),
            new GiftItemName(item.Name),
            // GiftItemDescription.Rehydrate/GiftItemUrl.Rehydrate, not the validating
            // constructors: GL-74's rules are new, so an already-persisted item may hold a value
            // one of them would now reject — each type's own doc comment has the reasoning,
            // mirroring ExpiryDate.Rehydrate above.
            item.Description is null ? null : GiftItemDescription.Rehydrate(item.Description),
            item.Url is null ? null : GiftItemUrl.Rehydrate(item.Url))));
}
