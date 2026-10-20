using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>Raised when an owner changes an item's priority. See <c>GiftListCreated</c> for why this never leaves the process directly.</summary>
public sealed record GiftItemPriorityChanged(
    GiftListId ListId, GiftItemId ItemId, GiftItemPriority Priority, DateTimeOffset ChangedAt) : IDomainEvent;
