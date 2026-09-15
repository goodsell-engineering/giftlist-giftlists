using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists;

/// <summary>
/// Thrown by <see cref="IGiftListRepository.UpdateAsync"/> when another writer persisted a
/// change to the same list first — the optimistic-concurrency guard behind GL-64 (two concurrent
/// <c>AddGiftItem</c> messages for one list both reading, both appending, and the second
/// <c>ReplaceOneAsync</c> silently discarding the first's item).
///
/// Since GL-68 only a change that writes a whole field (today, a rename) can raise this. Adding
/// or removing an item is applied as an element-scoped operation that cannot lose a concurrent
/// writer's element, so it has no reason to conflict and is not made to retry — see
/// <c>GiftListToUpdateMapper</c>'s own doc comment for why that is safe rather than merely
/// faster.
///
/// This is deliberately NOT a <c>Result</c>/<c>GiftListErrors</c> value: losing a race is not a
/// business rule an end user did anything wrong to trigger (contrast <c>giftlist.expired</c>),
/// it is a transient condition the SAME message succeeds at on a second attempt. Application
/// never catches this — it propagates out of the interactor, through the thin Rebus handler
/// (CONVENTIONS.md "Messaging"'s "no business logic" cuts both ways: catching this here to retry would
/// be reinventing what the message bus already does), so Rebus's own retry strategy redelivers
/// the message. The redelivered handler re-reads the now-current version and reapplies its
/// change on top, so nothing is lost — just, unlike a version-losing <c>$set</c> of the whole
/// document, redone once.
/// </summary>
public sealed class GiftListConcurrencyException(GiftListId listId)
    : Exception($"Gift list '{listId}' was modified by another writer first; retry against the current version.")
{
    public GiftListId ListId { get; } = listId;
}
