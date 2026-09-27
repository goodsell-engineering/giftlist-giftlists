namespace GiftLists.Contracts.GiftLists;

/// <summary>
/// Command: change or clear an item's description. Owner-only, fire-and-forget
/// (ARCHITECTURE.md "Command → event flow"), like <see cref="RemoveGiftItem"/>. A null
/// <see cref="Description"/> clears it. The outcome surfaces as
/// <c>GiftLists.Contracts.GiftLists.Events.GiftItemDescriptionChangedV1</c>.
/// </summary>
public sealed record ChangeGiftItemDescription(Guid ListId, Guid RequesterId, Guid ItemId, string? Description);
