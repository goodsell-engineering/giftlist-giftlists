namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>
/// Published after a new list is saved (ARCHITECTURE.md "Event catalogue": GiftLists → Gateway, Reservation).
/// The wire counterpart of the domain event
/// <c>GiftLists.Domain.GiftLists.Events.GiftListCreated</c> — mapped by an Infrastructure event
/// mapper, never published directly (ARCHITECTURE.md "Domain events are not integration events").
/// </summary>
public sealed record GiftListCreatedV1(
    Guid ListId,
    Guid OwnerId,
    string Name,
    DateTimeOffset ExpiresAt,
    string ShareToken,
    DateTimeOffset CreatedAt);
