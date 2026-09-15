namespace GiftLists.Domain.Common;

/// <summary>
/// An instant in this domain has MILLISECOND resolution. This normalises one to that resolution.
/// </summary>
/// <remarks>
/// <para>
/// That is a domain decision, not a storage detail: a gift list's expiry and creation time are
/// meaningful to the millisecond and no finer, so two instants that differ only below that are the
/// same instant here. Stating it in the Domain means every layer inherits one answer instead of
/// each rediscovering it.
/// </para>
/// <para>
/// It was found the way such rules usually are. .NET ticks are 100ns and the BSON dates
/// <c>GiftListDocument</c> stores (deliberately, so expiry can be range-queried) are milliseconds,
/// so an aggregate built from <see cref="DateTimeOffset.UtcNow"/> held up to 9999 ticks that
/// storage discarded, and a reloaded list did not equal the one saved. <c>ExpiryDate</c>'s equality
/// IS its value, so a round-tripped expiry compared unequal, and <c>HasExpired</c> could answer
/// differently either side of a reload — the boundary <c>GiftList.AddItem</c> enforces.
/// </para>
/// <para>
/// Normalising at construction rather than in the persistence mapper is what makes "saved equals
/// reloaded" true by construction: truncating only on the way out would fix reads while leaving a
/// freshly-built aggregate holding precision destined to vanish. Truncate, never round, so a value
/// can only move toward the past — rounding up could make an expiry that storage will report as
/// elapsed look un-elapsed in memory.
/// </para>
/// <para>
/// <c>Identity.Domain.Common.Timestamps</c> is the same rule, applied to
/// <c>Identity.Domain.Users.User.CreatedAt</c>, which carried the identical defect until GL-63
/// (raised by the Batch 11 review of this fix) closed it there too. This cannot move to
/// BuildingBlocks — Domain references nothing (CONVENTIONS.md "Project reference graph") — so it is a deliberate
/// per-service copy, exactly as <see cref="IDomainEvent"/> is. Tracked rather than left as a
/// silent asymmetry: see that copy's own doc comment, which cross-references this one.
/// </para>
/// </remarks>
public static class Timestamps
{
    /// <summary>Drops sub-millisecond ticks, preserving the offset.</summary>
    public static DateTimeOffset ToStoredPrecision(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond));
}
