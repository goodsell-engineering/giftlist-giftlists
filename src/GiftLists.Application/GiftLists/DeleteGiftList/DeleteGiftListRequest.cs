namespace GiftLists.Application.GiftLists.DeleteGiftList;

public sealed record DeleteGiftListRequest(Guid ListId, Guid RequesterId);
