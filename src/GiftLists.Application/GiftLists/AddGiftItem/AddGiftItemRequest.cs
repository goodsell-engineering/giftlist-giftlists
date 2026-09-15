namespace GiftLists.Application.GiftLists.AddGiftItem;

/// <summary><paramref name="ItemId"/> is client-generated, same rationale as <c>CreateGiftListRequest.ListId</c>.</summary>
public sealed record AddGiftItemRequest(
    Guid ListId,
    Guid RequesterId,
    Guid ItemId,
    string Name,
    string? Description,
    string? Url);
