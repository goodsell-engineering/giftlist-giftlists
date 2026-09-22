namespace GiftLists.Application.GiftLists.ChangeGiftListExpiry;

/// <summary><paramref name="RequesterId"/> is the caller's own id (from the JWT, per ARCHITECTURE.md "Auth & sharing"), checked against the list's <c>OwnerId</c> before the expiry is moved.</summary>
public sealed record ChangeGiftListExpiryRequest(Guid ListId, Guid RequesterId, DateTimeOffset ExpiresAt);
