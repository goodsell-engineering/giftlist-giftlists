namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>Published after an item is removed (ARCHITECTURE.md "Event catalogue"). Wire counterpart of <c>GiftLists.Domain.GiftLists.Events.GiftItemRemoved</c>.</summary>
public sealed record GiftItemRemovedV1(Guid ListId, Guid ItemId, DateTimeOffset RemovedAt);
