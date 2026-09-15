namespace GiftLists.Contracts.GiftLists;

/// <summary>Command: rename an existing list. Fire-and-forget (ARCHITECTURE.md "Command → event flow"); <see cref="RequesterId"/> is the caller's own id, checked against the list's owner.</summary>
public sealed record RenameGiftList(Guid ListId, Guid RequesterId, string Name);
