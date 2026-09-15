using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists.Events;

/// <summary>
/// Raised when a new gift list is created. Never leaves the process — an Infrastructure mapper
/// translates this into the wire-facing <c>GiftListCreatedV1</c> integration event for
/// publication (ARCHITECTURE.md "Domain events are not integration events").
/// </summary>
public sealed record GiftListCreated(
    GiftListId ListId,
    OwnerId OwnerId,
    GiftListName Name,
    ExpiryDate Expiry,
    ShareToken ShareToken,
    DateTimeOffset CreatedAt) : IDomainEvent;
