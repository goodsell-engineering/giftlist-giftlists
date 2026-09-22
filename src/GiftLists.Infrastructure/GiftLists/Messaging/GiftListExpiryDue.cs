namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// The message <see cref="GiftListExpirySaga"/> defers to itself, due at the list's expiry
/// (ARCHITECTURE.md "Sagas: list expiry"). Private to this service — it travels only from the
/// <c>giftlist</c> queue, through Rebus's Mongo-backed timeout store, back to the same queue —
/// so it lives here and not in <c>GiftLists.Contracts</c>: nobody else may send it, and it is not
/// part of the wire surface (ARCHITECTURE.md "Contracts: each service owns and publishes its own").
/// </summary>
/// <param name="ExpiresAt">
/// The expiry this timeout was scheduled for. Rebus cannot cancel a deferred message, so a
/// rescheduled saga does not try to: it lets every timeout arrive and acts only on the one whose
/// <paramref name="ExpiresAt"/> is still the expiry it holds. A superseded timeout is recognised
/// by this field, not by anything stored.
/// </param>
internal sealed record GiftListExpiryDue(Guid ListId, DateTimeOffset ExpiresAt);
