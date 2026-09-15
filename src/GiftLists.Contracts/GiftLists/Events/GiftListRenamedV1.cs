namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>Published after a rename is saved (ARCHITECTURE.md "Event catalogue"). Wire counterpart of <c>GiftLists.Domain.GiftLists.Events.GiftListRenamed</c>.</summary>
public sealed record GiftListRenamedV1(Guid ListId, string Name, DateTimeOffset RenamedAt);
