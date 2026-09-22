namespace GiftLists.Application.GiftLists.ChangeGiftListExpiry;

public sealed record ChangeGiftListExpiryResponse(Guid ListId, DateTimeOffset ExpiresAt);
