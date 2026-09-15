namespace GiftLists.Contracts.GiftLists;

/// <summary>Command: delete an existing list. Fire-and-forget (ARCHITECTURE.md "Command → event flow"); <see cref="RequesterId"/> is the caller's own id, checked against the list's owner.</summary>
public sealed record DeleteGiftList(Guid ListId, Guid RequesterId);
