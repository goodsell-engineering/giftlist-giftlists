using BuildingBlocks.Results;
using GiftLists.Application.GiftLists;
using GiftLists.Domain.GiftLists;
using MongoDB.Driver;

namespace GiftLists.Infrastructure.GiftLists.Persistence;

internal sealed class GiftListRepository : IGiftListRepository
{
    public const string CollectionName = "giftLists";

    private readonly IMongoCollection<GiftListDocument> _giftLists;

    public GiftListRepository(IMongoDatabase database)
    {
        _giftLists = database.GetCollection<GiftListDocument>(CollectionName);
    }

    public async Task<GiftList?> FindByIdAsync(GiftListId id, CancellationToken cancellationToken)
    {
        var document = await _giftLists
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : GiftListToDocumentMapper.ToAggregate(document);
    }

    public async Task<Result> AddAsync(GiftList list, CancellationToken cancellationToken)
    {
        var document = GiftListToDocumentMapper.ToDocument(list);

        try
        {
            await _giftLists.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Expected outcome, not a bug — the unique index on shareToken (or, vanishingly
            // rarely, a client-generated id collision) caught it (CONVENTIONS.md "Errors"). Publishing
            // domain events is not this port's job; see GiftListEventPublisher.
            return GiftListErrors.Duplicate;
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies only what changed, as partial Mongo operators derived from the aggregate's own
    /// domain events (GL-68) — <c>$push</c> for an added item, <c>$pull</c> for a removed one,
    /// <c>$set</c> for a rename — rather than rewriting the whole document. GL-64 made a racing
    /// write fail loudly instead of silently dropping the loser's item, but it did so with a
    /// version-guarded whole-document replace, so EVERY pair of concurrent writes to one list
    /// conflicted and one was always redone, even when the two changes could not possibly have
    /// touched each other. Two adds of different items now both succeed outright.
    ///
    /// <see cref="GiftListToUpdateMapper"/> owns the interesting half of this — which operator,
    /// which precondition, and specifically why the per-list version guard narrowed to
    /// field-scoped writes instead of being kept or dropped wholesale. Read that before changing
    /// anything here.
    /// </summary>
    /// <exception cref="GiftListConcurrencyException">Another writer's change landed first (transient — see that type).</exception>
    /// <exception cref="GiftListDeletedDuringUpdateException">The list was deleted between this operation's read and its write (terminal).</exception>
    public async Task UpdateAsync(GiftList list, CancellationToken cancellationToken)
    {
        var update = GiftListToUpdateMapper.ToUpdate(list);
        if (update is null)
        {
            // Nothing was mutated, so there is nothing to write — unless the counter says
            // otherwise, in which case the caller published and cleared its domain events before
            // saving, and this method has just been handed an aggregate with no record of what it
            // changed. That silently persists nothing, which is the one failure mode this design
            // trades for its concurrency (IGiftListRepository.UpdateAsync's own doc comment states
            // the ordering as part of the port's contract); a bug, so it throws (CONVENTIONS.md "Errors").
            if (list.Version != list.OriginalVersion)
            {
                throw new InvalidOperationException(
                    $"Gift list '{list.Id}' was mutated but carries no domain events, so there is " +
                    "nothing to persist. UpdateAsync must be called before the aggregate's events " +
                    "are published and cleared — they are the record of what to write.");
            }

            return;
        }

        var result = await _giftLists.UpdateOneAsync(
            update.Precondition, update.Change, cancellationToken: cancellationToken);
        if (result.MatchedCount != 0)
        {
            return;
        }

        // MatchedCount == 0 has two causes that need different handling, and Mongo reports them
        // identically: a precondition did not hold (transient — another writer got there first,
        // clears on redelivery), or the list was deleted between this operation's read and its
        // write (terminal). One extra read tells them apart, and only on the failure path, so the
        // happy path is unaffected.
        //
        // Every failed precondition is a conflict, INCLUDING "this item is already there". That
        // case is tempting to absorb as a success — storage-wise the desired state does hold, and
        // an earlier revision of this method did exactly that. It is wrong, and not visibly so
        // from here: the caller publishes on the line after this one, unconditionally
        // (AddGiftItemInteractor), so returning normally means a duplicate GiftItemAddedV1 goes
        // out carrying its own later AddedAt. The Gateway projection recognises an exact
        // redelivery only by an equal timestamp, so it applies that as a real write, advances the
        // item's UpdatedAt, and thereafter rejects a GiftItemRemovedV1 timestamped between the
        // two adds — a removed gift silently reappearing (GL-68 review). The version guard used
        // to prevent this as a side effect of failing the write; throwing keeps that property
        // rather than rediscovering it. The retry re-reads, GL-20's guard sees the item, and the
        // redelivery no-ops without publishing.
        var stillExists = await _giftLists.CountDocumentsAsync(
            d => d.Id == list.Id.Value, cancellationToken: cancellationToken) > 0;

        throw stillExists
            ? new GiftListConcurrencyException(list.Id)
            : new GiftListDeletedDuringUpdateException(list.Id);
    }

    public Task DeleteAsync(GiftListId id, CancellationToken cancellationToken) =>
        _giftLists.DeleteOneAsync(d => d.Id == id.Value, cancellationToken);

    /// <summary>
    /// The uniqueness guarantee behind the share link (ARCHITECTURE.md "Data model" — "shareToken (unique
    /// idx, opaque)") — a correctness requirement, not an optimisation. Applied at startup by
    /// <c>GiftListsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync</c>, not left to
    /// be inferred from application code (CONVENTIONS.md "Persistence").
    /// </summary>
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<GiftListDocument>(CollectionName);
        var shareTokenIndex = new CreateIndexModel<GiftListDocument>(
            Builders<GiftListDocument>.IndexKeys.Ascending(d => d.ShareToken),
            new CreateIndexOptions { Unique = true, Name = "shareToken_unique" });

        return collection.Indexes.CreateOneAsync(shareTokenIndex, cancellationToken: cancellationToken);
    }
}
