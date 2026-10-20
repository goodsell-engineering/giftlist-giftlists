namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>
/// Published after an item's priority is changed (ARCHITECTURE.md "Event catalogue"). Wire
/// counterpart of <c>GiftLists.Domain.GiftLists.Events.GiftItemPriorityChanged</c>.
/// <see cref="Priority"/> is the resulting value, always 1–5. Not published when the new value
/// equals the current one.
/// </summary>
public sealed record GiftItemPriorityChangedV1(Guid ListId, Guid ItemId, int Priority, DateTimeOffset ChangedAt);
