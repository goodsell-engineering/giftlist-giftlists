namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>
/// Published after a list's expiry is moved (ARCHITECTURE.md "Event catalogue"). Wire counterpart
/// of <c>GiftLists.Domain.GiftLists.Events.GiftListExpiryChanged</c>. Any consumer projecting
/// <c>ExpiresAt</c> from <see cref="GiftListCreatedV1"/> must apply this too, or its copy goes
/// stale the first time an owner moves an expiry.
/// </summary>
public sealed record GiftListExpiryChangedV1(Guid ListId, DateTimeOffset ExpiresAt, DateTimeOffset ChangedAt);
