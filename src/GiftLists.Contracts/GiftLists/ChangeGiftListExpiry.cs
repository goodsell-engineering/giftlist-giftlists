namespace GiftLists.Contracts.GiftLists;

/// <summary>
/// Command: ask GiftLists to move a list's expiry. Fire-and-forget, like <see cref="RenameGiftList"/>
/// (ARCHITECTURE.md "Command → event flow"). The outcome surfaces as
/// <c>GiftLists.Contracts.GiftLists.Events.GiftListExpiryChangedV1</c>, which the expiry saga
/// (ARCHITECTURE.md "Sagas: list expiry") reschedules against. Added by GL-41 as the producer
/// side of "expiry change reschedules"; no caller sends it yet.
/// </summary>
public sealed record ChangeGiftListExpiry(Guid ListId, Guid RequesterId, DateTimeOffset ExpiresAt);
