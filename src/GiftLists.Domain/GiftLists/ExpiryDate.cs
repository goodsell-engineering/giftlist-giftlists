using GiftLists.Domain.Common;

namespace GiftLists.Domain.GiftLists;

/// <summary>
/// A gift list's expiry (CONVENTIONS.md "Domain modelling" — value object; "must be future at creation"). Expiry
/// is a query-time predicate, never a Mongo TTL index (ARCHITECTURE.md "Auth & sharing") — an expired list
/// stays visible to its owner, it just becomes read-only for new items (<see cref="HasExpired"/>
/// is what <c>GiftList.AddItem</c> checks).
/// </summary>
public sealed class ExpiryDate : IEquatable<ExpiryDate>
{
    /// <summary>
    /// Validates "must be future at creation" once, at construction — the same reason
    /// <c>Identity.Domain.Users.Email</c>'s constructor validates format once instead of every
    /// caller re-checking. <paramref name="now"/> comes from the caller's clock port rather than
    /// <see cref="DateTimeOffset.UtcNow"/>: Domain has no project references (CONVENTIONS.md "Project reference graph")
    /// and must stay deterministic for tests, same as <c>Identity.Domain.Users.User.Register</c>'s
    /// <c>registeredAt</c> parameter.
    /// </summary>
    public ExpiryDate(DateTimeOffset value, DateTimeOffset now)
    {
        // Normalise BEFORE validating, so the rule is enforced against the value that will
        // actually be stored. Validating the un-truncated value could admit an expiry that is
        // future by less than a millisecond and then persist it as already elapsed.
        value = Timestamps.ToStoredPrecision(value);

        if (!IsInFuture(value, now))
        {
            throw new ArgumentException("Expiry must be in the future.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Rehydration-only constructor: a list loaded back out of storage may legitimately already
    /// be expired — "must be future" is a creation-time rule, not an always-true invariant, so
    /// reloading it must not re-run that check (CONVENTIONS.md "Domain modelling"). Only the Infrastructure
    /// persistence mapper calls <see cref="Rehydrate"/>, never this constructor.
    /// </summary>
    private ExpiryDate(DateTimeOffset value) => Value = Timestamps.ToStoredPrecision(value);

    public DateTimeOffset Value { get; }

    /// <summary>
    /// The same rule the primary constructor enforces, exposed as a non-throwing predicate so an
    /// Application-ring validator can reject a past expiry as a <c>Result</c> before it ever
    /// reaches the constructor (CONVENTIONS.md "Domain modelling", mirroring <c>Email.IsValidFormat</c>).
    /// </summary>
    // Normalised before comparing, so this predicate and the constructor enforce the rule on the
    // SAME value. They did not: the constructor truncated then validated while this tested the raw
    // input, so for any expiry in (now, now+1ms) the Application validator returned Success and the
    // constructor then threw ArgumentException — escaping the handler into Rebus's error queue
    // instead of returning the giftlist.expiry_invalid Result the design calls for. Two green tests
    // pinned both halves of that contradiction and neither could see the other (Batch 11 review).
    public static bool IsInFuture(DateTimeOffset value, DateTimeOffset now) =>
        Timestamps.ToStoredPrecision(value) > now;

    /// <summary>"you cannot add an item to an expired list" is the domain invariant (CONVENTIONS.md "Domain modelling") that <c>GiftList.AddItem</c> enforces.</summary>
    public bool HasExpired(DateTimeOffset now) => Value <= now;

    public static ExpiryDate Rehydrate(DateTimeOffset value) => new(value);

    public bool Equals(ExpiryDate? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as ExpiryDate);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString("O");
}
