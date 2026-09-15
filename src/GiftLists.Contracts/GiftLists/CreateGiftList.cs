namespace GiftLists.Contracts.GiftLists;

/// <summary>
/// Command: ask GiftLists to create a new list. Sent with <c>bus.Send()</c> and fire-and-forget
/// (ARCHITECTURE.md "Command → event flow" — the default; unlike Login/SignUp/ReserveGift, no caller is actively
/// waiting on a yes/no here). The Gateway generates <see cref="ListId"/> client-side so the SPA
/// can navigate to the new list before the read model, built from
/// <c>GiftLists.Contracts.GiftLists.Events.GiftListCreatedV1</c>, catches up.
/// </summary>
public sealed record CreateGiftList(Guid ListId, Guid OwnerId, string Name, DateTimeOffset ExpiresAt);
