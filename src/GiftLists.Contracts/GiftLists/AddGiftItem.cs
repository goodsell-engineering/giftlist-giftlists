namespace GiftLists.Contracts.GiftLists;

/// <summary>
/// Command: add an item to a list. Fire-and-forget (ARCHITECTURE.md "Command → event flow");
/// <see cref="ItemId"/> is client-generated, same rationale as <see cref="CreateGiftList.ListId"/>.
/// <see cref="Priority"/> is 1–5 (1 highest); null means the default, 3. Appended in 0.4.0 so
/// senders built against earlier versions keep working.
/// </summary>
public sealed record AddGiftItem(Guid ListId, Guid RequesterId, Guid ItemId, string Name, string? Description, string? Url, int? Priority = null);
