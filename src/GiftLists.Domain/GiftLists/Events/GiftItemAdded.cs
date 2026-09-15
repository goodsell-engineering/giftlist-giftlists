using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>Raised when an owner adds an item to their gift list. See <c>GiftListCreated</c> for why this never leaves the process directly.</summary>
public sealed record GiftItemAdded(
    GiftListId ListId,
    GiftItemId ItemId,
    GiftItemName Name,
    GiftItemDescription? Description,
    GiftItemUrl? Url,
    DateTimeOffset AddedAt) : IDomainEvent;
