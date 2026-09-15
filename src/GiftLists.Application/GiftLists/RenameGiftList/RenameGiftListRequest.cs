namespace GiftLists.Application.GiftLists.RenameGiftList;

/// <summary><paramref name="RequesterId"/> is the caller's own id (from the JWT, per ARCHITECTURE.md "Auth & sharing"), checked against the list's <c>OwnerId</c> before the rename is applied.</summary>
public sealed record RenameGiftListRequest(Guid ListId, Guid RequesterId, string Name);
