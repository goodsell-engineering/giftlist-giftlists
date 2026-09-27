namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>
/// Published after an item's description is changed or cleared (ARCHITECTURE.md "Event
/// catalogue"). Wire counterpart of
/// <c>GiftLists.Domain.GiftLists.Events.GiftItemDescriptionChanged</c>. <see cref="Description"/>
/// is the resulting value after trimming; null means cleared, so consumers never re-apply domain
/// rules.
/// </summary>
public sealed record GiftItemDescriptionChangedV1(Guid ListId, Guid ItemId, string? Description, DateTimeOffset ChangedAt);
