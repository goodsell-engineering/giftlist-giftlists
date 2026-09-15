using BuildingBlocks.Results;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists;

/// <summary>Port lives beside the domain it serves (CONVENTIONS.md "Folder structure"), not in a shared Abstractions bucket. One repository per aggregate root — no <c>IGiftItemRepository</c>; items are reached through <see cref="GiftList"/> (CONVENTIONS.md "Persistence").</summary>
public interface IGiftListRepository
{
    Task<GiftList?> FindByIdAsync(GiftListId id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a brand-new list. Returns <see cref="GiftListErrors.Duplicate"/> if the unique
    /// index on <c>shareToken</c> (ARCHITECTURE.md "Data model") rejects the insert, rather than throwing —
    /// an expected outcome is a value (CONVENTIONS.md "Errors"), even though a random 21-character
    /// base62 collision is vanishingly unlikely in practice.
    /// </summary>
    Task<Result> AddAsync(GiftList list, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the changes <paramref name="list"/> has recorded since it was loaded (rename,
    /// add/remove item), such that two racing updates cannot silently drop one another's (GL-64)
    /// and, where the two changes cannot affect each other at all, neither is made to wait on or
    /// redo the other (GL-68). Throws <see cref="GiftListConcurrencyException"/>, never a
    /// <c>Result</c>, when a change genuinely does conflict with one already persisted — see that
    /// exception's own doc comment for why this is a throw and what is expected to happen next.
    /// </summary>
    /// <remarks>
    /// Call this BEFORE publishing and clearing the aggregate's domain events: they are the
    /// record of what changed, and the adapter needs them to write only what changed rather than
    /// the whole document. Every interactor already saves first and publishes second
    /// (ARCHITECTURE.md "Event publishing: synchronous"), so this asks for nothing new — it makes the existing order
    /// load-bearing, and an aggregate arriving here mutated but eventless throws rather than
    /// silently persisting nothing.
    /// </remarks>
    /// <exception cref="GiftListConcurrencyException">
    /// Another writer's update landed first. The caller must not catch and swallow this — let it
    /// propagate so the message is retried.
    /// </exception>
    Task UpdateAsync(GiftList list, CancellationToken cancellationToken);

    Task DeleteAsync(GiftListId id, CancellationToken cancellationToken);
}
