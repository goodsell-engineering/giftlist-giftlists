namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>Published after an item is added (ARCHITECTURE.md "Event catalogue"). Wire counterpart of <c>GiftLists.Domain.GiftLists.Events.GiftItemAdded</c>.</summary>
public sealed record GiftItemAddedV1(
    Guid ListId,
    Guid ItemId,
    string Name,
    string? Description,
    string? Url,
    DateTimeOffset AddedAt);
