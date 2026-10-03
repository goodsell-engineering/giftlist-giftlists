namespace GiftLists.Application.GiftLists.ChangeGiftItemDescription;

public sealed record ChangeGiftItemDescriptionRequest(Guid ListId, Guid RequesterId, Guid ItemId, string? Description);
