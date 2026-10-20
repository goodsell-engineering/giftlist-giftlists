namespace GiftLists.Contracts.GiftLists;

/// <summary>
/// Command: change an item's priority. Owner-only, fire-and-forget (ARCHITECTURE.md "Command →
/// event flow"), like <see cref="ChangeGiftItemDescription"/>. <see cref="Priority"/> is 1–5
/// (1 highest). The outcome surfaces as
/// <c>GiftLists.Contracts.GiftLists.Events.GiftItemPriorityChangedV1</c>.
/// </summary>
public sealed record ChangeGiftItemPriority(Guid ListId, Guid RequesterId, Guid ItemId, int Priority);
