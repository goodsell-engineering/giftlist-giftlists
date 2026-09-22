using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>
/// Raised when an owner moves their gift list's expiry. See <c>GiftListCreated</c> for why this
/// never leaves the process directly. Carries the new expiry only — the expiry saga
/// (ARCHITECTURE.md "Sagas: list expiry") reschedules against it and needs nothing about the
/// old one, since a superseded timeout is recognised by comparing against the saga's own
/// current expiry, not by cancelling anything.
/// </summary>
public sealed record GiftListExpiryChanged(GiftListId ListId, ExpiryDate Expiry, DateTimeOffset ChangedAt) : IDomainEvent;
