using GiftLists.Domain.GiftLists;
using GiftLists.Domain.GiftLists.Events;
using MongoDB.Driver;

namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>
/// Turns the domain events an aggregate raised into the narrowest Mongo update that applies them
/// (GL-68). Source + "To" + target (CONVENTIONS.md "Naming"), the sibling of
/// <see cref="GiftListToDocumentMapper"/>: that one maps the whole aggregate, for
/// <see cref="GiftListRepository.AddAsync"/>, which genuinely writes a whole document; this one
/// maps only what changed.
///
/// <para><b>Why events.</b> <c>IGiftListRepository.UpdateAsync</c> takes a whole aggregate and
/// nothing else — the correct shape for the port (the Application ring hands over an object and
/// does not know how it is stored), but it leaves this side with no idea which of the stored
/// fields the caller actually touched. The domain events the aggregate already raises are
/// precisely that record, so they are read here rather than a "what changed" flag being invented
/// and kept in sync by hand.</para>
///
/// <para><b>Why the version guard is not applied to array operators.</b> The guard exists
/// because a whole-document replace carries the writer's own stale copy of <c>items</c>, so the
/// second of two racing writers erased the first's element (GL-64). <c>$push</c> and <c>$pull</c>
/// carry no copy of the array: the server appends to, or removes from, whatever the array holds
/// at write time. The lost update the guard was defending against is therefore structurally
/// impossible for them, and keeping a per-list version filter on them would serialise every write
/// to a list — exactly what GL-68 exists to stop. So they carry no version filter and, just as
/// importantly, do NOT write <c>version</c> either: bumping it would make a concurrent add
/// spuriously conflict a concurrent rename, reintroducing the serialisation through the back
/// door.</para>
///
/// <para><b>What replaces it.</b> The guard had two further jobs nobody had written down.
/// GL-20's redelivery guard in <c>AddGiftItemInteractor</c> is a read-then-check ("does this list
/// already hold this itemId?"), and two overlapping deliveries of the same message can both pass
/// it; the version guard rejected the second write, and the retry then saw the item and no-opped.
/// Drop the guard with nothing in its place and both pushes land — the duplicate item GL-20
/// fixed. Each <c>$push</c> therefore carries its own element-scoped precondition, "this list
/// does not already contain this itemId", and each <c>$pull</c> the mirror of it. Two operations
/// on DIFFERENT items still never obstruct each other, which is the point.</para>
///
/// <para><b>And the job underneath that one.</b> A failed precondition must be reported to the
/// caller as a conflict, not absorbed as "the desired state already holds" — see
/// <see cref="GiftListRepository.UpdateAsync"/>, which is where that is enforced and why. The
/// element-scoped precondition is therefore NOT "strictly stronger" than the per-list version
/// guard, as an earlier revision of this comment claimed: it is stronger about storage and
/// exactly as strong about messaging only because the write still fails loudly. Both properties
/// come from the failure, not from the precondition alone.</para>
///
/// <para><b>What keeps it.</b> A rename is a whole-field <c>$set</c>, not an element operation,
/// so nothing about GL-68 argues for loosening it and
/// <c>GiftListRepositoryTests.UpdateAsync_ShouldThrowConcurrencyException_WhenAnotherWriterSavedFirst</c>
/// (two racing renames) still means what it always meant. The guard narrows to field-scoped
/// writes rather than being dropped. One consequence worth stating plainly: the stored
/// <c>version</c> now counts field-scoped changes only, not every mutation — see
/// <see cref="GiftListDocument.Version"/>.</para>
/// </summary>
internal static class GiftListToUpdateMapper
{
    /// <summary>
    /// Returns <see langword="null"/> when the aggregate recorded no changes at all — there is
    /// then nothing to write, and issuing an update anyway would only invent a conflict against
    /// itself (<c>GiftList.Rehydrate</c>'s own doc comment promises it will not).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A bug in the caller or in this mapper, never a user-facing outcome (CONVENTIONS.md "Errors"):
    /// an unmapped domain event type, or a single save that both adds and removes items.
    /// </exception>
    public static GiftListUpdate? ToUpdate(GiftList list)
    {
        if (list.DomainEvents.Count == 0)
        {
            return null;
        }

        var addedItems = new List<GiftItemDocument>();
        var removedItemIds = new List<Guid>();
        string? renamedTo = null;

        foreach (var domainEvent in list.DomainEvents)
        {
            switch (domainEvent)
            {
                case GiftItemAdded added:
                    addedItems.Add(new GiftItemDocument
                    {
                        ItemId = added.ItemId.Value,
                        Name = added.Name.Value,
                        Description = added.Description?.Value,
                        Url = added.Url?.Value,
                    });
                    break;

                case GiftItemRemoved removed:
                    removedItemIds.Add(removed.ItemId.Value);
                    break;

                // Last one wins, rather than two colliding $set operators on one path. Two
                // renames before a single save is not something any interactor does, but "the
                // final name the aggregate holds" is the only answer that could be right.
                case GiftListRenamed renamed:
                    renamedTo = renamed.Name.Value;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"No Mongo update is defined for domain event '{domainEvent.GetType().Name}'. " +
                        "A new mutation on GiftList needs its operator adding here — failing loudly " +
                        "rather than silently persisting nothing.");
            }
        }

        if (addedItems.Count > 0 && removedItemIds.Count > 0)
        {
            throw new InvalidOperationException(
                "A single save cannot both add and remove gift items: $push and $pull would " +
                "collide on the same 'items' path. No interactor does this today; if one needs " +
                "to, it must save twice.");
        }

        var preconditions = new List<FilterDefinition<GiftListDocument>>
        {
            Builders<GiftListDocument>.Filter.Eq(d => d.Id, list.Id.Value),
        };
        var changes = new List<UpdateDefinition<GiftListDocument>>();

        if (addedItems.Count > 0)
        {
            changes.Add(Builders<GiftListDocument>.Update.PushEach(d => d.Items, addedItems));
            foreach (var added in addedItems)
            {
                // The element-scoped replacement for the version guard — see this type's own doc
                // comment. Written per item rather than as one $in so that adding two items at
                // once cannot be half-applied by a partially-satisfied precondition.
                preconditions.Add(Builders<GiftListDocument>.Filter.Not(
                    Builders<GiftListDocument>.Filter.ElemMatch(d => d.Items, i => i.ItemId == added.ItemId)));
            }
        }

        if (removedItemIds.Count > 0)
        {
            changes.Add(Builders<GiftListDocument>.Update.PullFilter(
                d => d.Items, i => removedItemIds.Contains(i.ItemId)));
            foreach (var removedItemId in removedItemIds)
            {
                // The mirror of the $push precondition, and needed for the same reason. $pull is
                // idempotent in storage — removing an absent element is a no-op — so it is
                // tempting to leave unguarded, but RemoveGiftItemInteractor publishes after this
                // returns, unconditionally, so an unguarded no-op write means a second
                // GiftItemRemovedV1 with its own later RemovedAt. Requiring the element to still
                // be there turns a duplicate delivery into a conflict, whose retry stops at the
                // interactor's own ItemNotFound check and publishes nothing.
                preconditions.Add(Builders<GiftListDocument>.Filter.ElemMatch(
                    d => d.Items, i => i.ItemId == removedItemId));
            }
        }

        if (renamedTo is not null)
        {
            changes.Add(Builders<GiftListDocument>.Update.Set(d => d.Name, renamedTo));
            // Field-scoped, so this update — and, conservatively, anything batched with it — is
            // version-guarded, and is the only kind of update that advances the stored counter.
            //
            // A caveat for whoever first writes an interactor that renames AND adds in one save
            // (none does today; UpdateAsync_ShouldSucceed_WhenTwoMutationsArePerformedBeforeOneSave
            // is the only caller shaped like this). The whole batch is one update, so if ANY
            // precondition fails the rename does not land either, and the caller is told only
            // "conflict". Recovery is then that caller's problem, and it is not automatic: a
            // retry that hits a GL-20-style "already done?" early return would return before
            // reapplying the rename, losing it silently. Splitting such a use case into two saves
            // avoids the question entirely (GL-68 review).
            preconditions.Add(Builders<GiftListDocument>.Filter.Eq(d => d.Version, list.OriginalVersion));
            changes.Add(Builders<GiftListDocument>.Update.Set(d => d.Version, list.Version));
        }

        return new GiftListUpdate(
            Builders<GiftListDocument>.Filter.And(preconditions),
            Builders<GiftListDocument>.Update.Combine(changes));
    }
}
