using GiftLists.Domain.Common;
using GiftLists.Domain.GiftLists.Events;

namespace GiftLists.Domain.GiftLists;

/// <summary>
/// The GiftLists service's one aggregate root (ARCHITECTURE.md "Data model" — <c>giftlist.giftLists</c>).
/// <see cref="GiftItem"/>s are reached only through this root — no reservation data lives here;
/// that is structurally impossible by design (ARCHITECTURE.md "Data model"), owned entirely by the
/// Reservation service instead.
/// </summary>
public sealed class GiftList
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<GiftItem> _items;

    private GiftList(
        GiftListId id,
        OwnerId ownerId,
        GiftListName name,
        ExpiryDate expiry,
        ShareToken shareToken,
        DateTimeOffset createdAt,
        long version,
        List<GiftItem> items)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        Expiry = expiry;
        ShareToken = shareToken;
        // Truncated to what Mongo can round-trip — see Timestamps.ToStoredPrecision.
        CreatedAt = Timestamps.ToStoredPrecision(createdAt);
        Version = version;
        OriginalVersion = version;
        _items = items;
    }

    public GiftListId Id { get; }

    public OwnerId OwnerId { get; }

    public GiftListName Name { get; private set; }

    public ExpiryDate Expiry { get; private set; }

    public ShareToken ShareToken { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Incremented on every state-changing call below (never on <see cref="Create"/> itself,
    /// which starts a list at 0). This is a plain optimistic-concurrency counter, not a Mongo
    /// concept — GiftListRepository.UpdateAsync's own doc comment has the mechanism it backs.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// The version this instance was loaded at, never incremented — what a filtered update must
    /// match against storage.
    /// </summary>
    /// <remarks>
    /// The repository used to derive that as <c>Version - 1</c>, which silently encoded a rule
    /// nobody had stated: exactly one mutation per save. Two mutations before one save expected
    /// <c>stored + 1</c> and conflicted spuriously; ZERO mutations expected <c>stored - 1</c> and
    /// also conflicted — directly contradicting <see cref="Rehydrate"/>'s own doc comment, which
    /// promised a reload-then-save without mutation would not manufacture a conflict against
    /// itself. No caller hits either case today, so it was a trap for the next writer wearing a
    /// comment saying the trap was not there (Batch 12 review). Carrying the loaded version
    /// explicitly removes the arithmetic and the unstated rule with it.
    /// </remarks>
    public long OriginalVersion { get; }

    public IReadOnlyCollection<GiftItem> Items => _items;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Creates a brand-new list, raising <see cref="GiftListCreated"/>. The only path that
    /// raises that event — <see cref="Rehydrate"/> never does, since loading an existing list
    /// back out of storage is not something newly happening (CONVENTIONS.md "Domain modelling", mirroring
    /// <c>Identity.Domain.Users.User.Register</c>).
    /// </summary>
    public static GiftList Create(
        GiftListId id,
        OwnerId ownerId,
        GiftListName name,
        ExpiryDate expiry,
        ShareToken shareToken,
        DateTimeOffset createdAt)
    {
        var list = new GiftList(id, ownerId, name, expiry, shareToken, createdAt, version: 0, []);
        // list.CreatedAt, NOT the raw createdAt parameter: the constructor normalised it, and an
        // event carrying the un-normalised value would tell Gateway and Reservation something that
        // disagrees with what giftlist.giftLists holds — the same drift this normalisation exists to
        // remove, one hop further out where it is far harder to notice (Batch 11 review).
        list._domainEvents.Add(new GiftListCreated(id, ownerId, name, expiry, shareToken, list.CreatedAt));
        return list;
    }

    /// <summary>
    /// Rebuilds a <see cref="GiftList"/> from persisted state. Called only by the Infrastructure
    /// mapper (CONVENTIONS.md "Domain modelling" — no public parameterless constructor; rehydration goes through
    /// the mapper). Never raises domain events. <paramref name="version"/> is whatever the
    /// document held (<see cref="Version"/>'s own doc comment) — carried through unmutated so a
    /// caller that reloads and immediately saves without mutating does not manufacture a
    /// conflict against itself.
    /// </summary>
    public static GiftList Rehydrate(
        GiftListId id,
        OwnerId ownerId,
        GiftListName name,
        ExpiryDate expiry,
        ShareToken shareToken,
        DateTimeOffset createdAt,
        long version,
        IEnumerable<GiftItem> items) =>
        new(id, ownerId, name, expiry, shareToken, createdAt, version, items.ToList());

    /// <summary>
    /// Renames the list, raising <see cref="GiftListRenamed"/>. Ownership ("is the caller
    /// allowed to do this") is checked by the interactor before this is called, not here — see
    /// the interactors' own doc comments for why that check sits in Application rather than
    /// Entities for this aggregate.
    /// </summary>
    public void Rename(GiftListName name, DateTimeOffset renamedAt)
    {
        Name = name;
        Version++;
        // Normalised for the same reason Create's constructor normalises CreatedAt: an instant in
        // this domain has millisecond resolution (Timestamps.ToStoredPrecision), and every event
        // this aggregate raises must agree with that, not only the one field that happens to be
        // persisted (GL-66).
        _domainEvents.Add(new GiftListRenamed(Id, name, Timestamps.ToStoredPrecision(renamedAt)));
    }

    /// <summary>
    /// Marks the list deleted, raising <see cref="GiftListDeleted"/>. Carries no state change of
    /// its own — the interactor removes the persisted document entirely after this returns
    /// (ARCHITECTURE.md "Event publishing: synchronous" — save/delete first, publish second), so there is no "IsDeleted"
    /// flag to keep in sync with a document that no longer exists.
    /// </summary>
    public void Delete(DateTimeOffset deletedAt)
    {
        Version++;
        // See Rename's own comment — GL-66.
        _domainEvents.Add(new GiftListDeleted(Id, Timestamps.ToStoredPrecision(deletedAt)));
    }

    /// <summary>
    /// Adds an item, raising <see cref="GiftItemAdded"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// "you cannot add an item to an expired list" is a domain invariant, not a UI check
    /// (CONVENTIONS.md "Domain modelling") — enforced here, not only in the interactor, so no
    /// future caller of this aggregate can bypass it by forgetting to check first. The
    /// interactor is still expected to check <see cref="ExpiryDate.HasExpired"/> itself and
    /// return a <c>GiftListErrors.Expired</c> <c>Result</c> before ever reaching this method
    /// (the expected, user-facing outcome — CONVENTIONS.md "Errors"); this throw is the backstop
    /// for a missed check, the same relationship <c>Email.IsValidFormat</c> has to its
    /// constructor.
    /// </exception>
    public void AddItem(GiftItemId itemId, GiftItemName name, GiftItemDescription? description, GiftItemUrl? url, DateTimeOffset addedAt)
    {
        if (Expiry.HasExpired(addedAt))
        {
            throw new InvalidOperationException(
                "Cannot add an item to an expired gift list — \"you cannot add an item to an expired " +
                "list\" is a domain invariant (CONVENTIONS.md \"Domain modelling\"); the caller must " +
                "check GiftList.Expiry.HasExpired before calling this.");
        }

        var item = GiftItem.Create(itemId, name, description, url);
        _items.Add(item);
        Version++;
        // See Rename's own comment — GL-66. The expiry check above intentionally uses the raw
        // addedAt: that is a "was this instant before or after expiry" comparison, not something
        // this aggregate stores or publishes, so it is unaffected by storage precision.
        _domainEvents.Add(new GiftItemAdded(Id, itemId, name, description, url, Timestamps.ToStoredPrecision(addedAt)));
    }

    /// <summary>
    /// Removes an item, raising <see cref="GiftItemRemoved"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The caller is expected to check the item exists (and return
    /// <c>GiftListErrors.ItemNotFound</c> as a <c>Result</c> if not — CONVENTIONS.md "Errors") before
    /// calling this; reaching this throw indicates a missed check, not a user mistake.
    /// </exception>
    public void RemoveItem(GiftItemId itemId, DateTimeOffset removedAt)
    {
        var removed = _items.RemoveAll(i => i.Id == itemId);
        if (removed == 0)
        {
            throw new InvalidOperationException($"No item '{itemId}' exists on gift list '{Id}'.");
        }

        Version++;
        // See Rename's own comment — GL-66.
        _domainEvents.Add(new GiftItemRemoved(Id, itemId, Timestamps.ToStoredPrecision(removedAt)));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
