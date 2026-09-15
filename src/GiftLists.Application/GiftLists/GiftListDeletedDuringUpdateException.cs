using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists;

/// <summary>
/// The list being updated no longer exists — it was deleted between this operation's read and its
/// write.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from <see cref="GiftListConcurrencyException"/> because the two are NOT the same kind
/// of failure, though Mongo reports both as <c>MatchedCount == 0</c>. A lost race is transient: the
/// same message succeeds on a later delivery, because the winner's write is now the version this
/// one will read. A deleted list is terminal: no number of retries brings it back, and the correct
/// final state is that the update simply does not happen.
/// </para>
/// <para>
/// Conflating them let <c>GiftListConcurrencyException</c>'s doc comment claim, wrongly, that the
/// condition always clears on retry (Batch 12 review). Separating them keeps that claim true of
/// the type that makes it, and puts an accurate exception name on the message that ends up in
/// Rebus's error queue.
/// </para>
/// <para>
/// KNOWN LIMITATION: this is still thrown rather than suppressed, so Rebus spends its remaining
/// delivery attempts on a condition that cannot clear before parking the message. That is wasteful
/// but not incorrect, and the alternative — swallowing here — would hide a genuinely surprising
/// interleaving from the error queue entirely. Suppressing retries for a terminal fault is a
/// broader change to the messaging configuration than this fix warrants.
/// </para>
/// </remarks>
public sealed class GiftListDeletedDuringUpdateException(GiftListId id)
    : Exception($"Gift list '{id.Value}' was deleted while an update to it was in flight.")
{
    public GiftListId Id { get; } = id;
}
