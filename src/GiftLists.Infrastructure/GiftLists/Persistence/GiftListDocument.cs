namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>
/// The Mongo-facing shape of a gift list (ARCHITECTURE.md "Data model" — <c>giftlist.giftLists</c>), kept
/// separate from the <c>GiftLists.Domain.GiftLists.GiftList</c> aggregate (ARCHITECTURE.md "Data that crosses boundaries" —
/// no <c>[Bson*]</c> attributes in Domain). No attributes are needed here either: <see cref="Id"/>
/// auto-maps to <c>_id</c> by the driver's own default convention, and field names are
/// camelCased by the shared <c>BuildingBlocks.Persistence.MongoConventions</c> pack registered
/// once at startup.
///
/// <see cref="ExpiresAt"/>/<see cref="CreatedAt"/> are <see cref="DateTime"/>, not the
/// aggregate's own <see cref="DateTimeOffset"/> — see
/// <c>Identity.Infrastructure.Users.Persistence.UserDocument.CreatedAt</c>'s own doc comment for
/// why (the driver's default <see cref="DateTimeOffset"/> representation doesn't range-query
/// like a native BSON date). Both are always UTC (sourced from <c>IClock.UtcNow</c>), so nothing
/// is lost by storing them as one.
/// </summary>
public sealed class GiftListDocument
{
    public required Guid Id { get; init; }

    public required Guid OwnerId { get; init; }

    public required string Name { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public required string ShareToken { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Optimistic-concurrency counter (GL-64). <see cref="GiftListRepository.UpdateAsync"/> is
    /// the only writer that filters on it.
    ///
    /// Since GL-68 it is an opaque guard token, not a count of anything. Only a field-scoped
    /// write (today, a rename) guards on and advances it; element-scoped <c>$push</c>/<c>$pull</c>
    /// updates leave it untouched by design — bumping it would make a concurrent add spuriously
    /// conflict a concurrent rename, which is the serialisation GL-68 removed. So it does not
    /// mirror <c>GiftList.Version</c> (a list whose items changed reads back LOWER than the
    /// in-memory aggregate that changed them), and it does not count field-scoped changes either:
    /// the value written is the aggregate's total mutation count, so one save that renames AND
    /// adds advances it by 2. All the guard needs is that it changes when a rename lands and that
    /// two writers never derive the same next value, both of which hold. If anything ever needs
    /// "how many times has this list changed", it needs a different field, not this one.
    /// <see cref="GiftListRepository.AddAsync"/> inserts whatever the
    /// aggregate's counter currently reads — normally 0 for a freshly created list, but NOT
    /// always: a caller that mutates before the first save inserts a higher number, which the
    /// integration suite's own seeding helper does (Batch 12 review corrected an earlier comment
    /// here claiming it is always 0).
    /// </summary>
    public required long Version { get; init; }

    public required List<GiftItemDocument> Items { get; init; }
}
