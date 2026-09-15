namespace GiftLists.Application.GiftLists.CreateGiftList;

public sealed record CreateGiftListResponse(Guid ListId, string ShareToken, DateTimeOffset ExpiresAt);
