namespace GiftLists.Contracts.GiftLists.Events;

/// <summary>Published after a list is deleted (ARCHITECTURE.md "Event catalogue"). Wire counterpart of <c>GiftLists.Domain.GiftLists.Events.GiftListDeleted</c>.</summary>
public sealed record GiftListDeletedV1(Guid ListId, DateTimeOffset DeletedAt);
