namespace GiftLists.Contracts.GiftLists;

/// <summary>Command: add an item to a list. Fire-and-forget (ARCHITECTURE.md "Command → event flow"); <see cref="ItemId"/> is client-generated, same rationale as <see cref="CreateGiftList.ListId"/>.</summary>
public sealed record AddGiftItem(Guid ListId, Guid RequesterId, Guid ItemId, string Name, string? Description, string? Url);
