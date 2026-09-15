namespace GiftLists.Application.GiftLists.RemoveGiftItem;

public sealed record RemoveGiftItemRequest(Guid ListId, Guid RequesterId, Guid ItemId);
