namespace GiftLists.Application.GiftLists.CreateGiftList;

/// <summary>
/// <paramref name="ListId"/> is client-generated (ARCHITECTURE.md "Command → event flow") — the Gateway mints it so
/// the SPA can navigate to the new list before the read model catches up.
/// </summary>
public sealed record CreateGiftListRequest(Guid ListId, Guid OwnerId, string Name, DateTimeOffset ExpiresAt);
