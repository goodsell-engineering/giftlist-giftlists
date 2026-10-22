namespace GiftLists.Application.GiftLists.ChangeGiftItemPriority;

public sealed record ChangeGiftItemPriorityRequest(Guid ListId, Guid RequesterId, Guid ItemId, int Priority);
