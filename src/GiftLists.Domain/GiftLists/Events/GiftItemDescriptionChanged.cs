using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>Raised when an owner changes or clears an item's description. See <c>GiftListCreated</c> for why this never leaves the process directly.</summary>
public sealed record GiftItemDescriptionChanged(
    GiftListId ListId, GiftItemId ItemId, GiftItemDescription? Description, DateTimeOffset ChangedAt) : IDomainEvent;
