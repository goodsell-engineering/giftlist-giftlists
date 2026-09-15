using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>Raised when an owner removes an item from their gift list. See <c>GiftListCreated</c> for why this never leaves the process directly.</summary>
public sealed record GiftItemRemoved(GiftListId ListId, GiftItemId ItemId, DateTimeOffset RemovedAt) : IDomainEvent;
