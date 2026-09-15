namespace GiftLists.Contracts.GiftLists;

/// <summary>Command: remove an item from a list. Fire-and-forget (ARCHITECTURE.md "Command → event flow").</summary>
public sealed record RemoveGiftItem(Guid ListId, Guid RequesterId, Guid ItemId);
