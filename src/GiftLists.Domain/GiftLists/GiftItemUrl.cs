namespace GiftLists.Domain.GiftLists;

/// <summary>
/// Where a gift can be found online (CONVENTIONS.md "Domain modelling" — value object for anything with a rule;
/// GL-74). Optional, and absence is modelled by a null <see cref="GiftItem.Url"/> reference
/// rather than a "blank but valid" state: the proto field is <c>optional string url</c> and
/// <see cref="GiftItem.Create"/> has always taken a nullable url, so "no URL" must stay a
/// non-outcome rather than becoming a validation failure the moment this type exists.
/// </summary>
/// <remarks>
/// "Absolute http/https only" rejects every vector GL-74 traced through the sibling rings: a
/// scheme-less, protocol-relative value ("//evil.example/") has no scheme at all and fails
/// <see cref="Uri.TryCreate(string?,UriKind,out Uri?)"/> in <see cref="UriKind.Absolute"/> mode
/// outright; "data:", "vbscript:" and any other scheme parse fine as an absolute URI but are
/// rejected by the explicit scheme check below; "javascript:" is also rejected here even though
/// React already blocks it at render time (GL-74's own verification), because this type's job is
/// "is this a URL we should have stored", not "is this safe to interpolate into one specific
/// renderer".
/// </remarks>
public sealed class GiftItemUrl : IEquatable<GiftItemUrl>
{
    public const int MaxLength = 2048;

    /// <summary>Validates a URL an owner is submitting right now.</summary>
    public GiftItemUrl(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Value must be at most {MaxLength} characters.", nameof(value));
        }

        if (!IsAbsoluteHttpUrl(trimmed))
        {
            throw new ArgumentException("Value must be an absolute http or https URL.", nameof(value));
        }

        Value = trimmed;
    }

    // Distinct arity from the validating constructor above purely to keep the two constructors
    // distinguishable by signature — trustedValue is never read for anything other than being
    // assigned, which is the whole point: Rehydrate below trusts it rather than re-checking it.
    private GiftItemUrl(string trustedValue, bool _) => Value = trustedValue;

    public string Value { get; }

    /// <summary>
    /// Rehydration-only, trusting storage rather than re-validating it — mirrors
    /// <see cref="ExpiryDate.Rehydrate"/>. "Absolute http/https only" is a write-time rule GL-74
    /// introduces well after items with a completely unchecked <c>Url</c> may already be
    /// persisted (this was a bare <c>string?</c> until now); re-validating on every read would
    /// turn a stricter rule into an outage for whichever already-stored list happens to hold a
    /// value the new rule rejects. <see cref="GiftItem.Create"/>'s own doc comment explains why
    /// it stays a single factory even though this constructor pair means the underlying value
    /// object, unlike <see cref="GiftItemName"/>, now needs the split: only this type's rule
    /// tightened after data existed, so only this type carries a trusted path. Only the
    /// Infrastructure persistence mapper calls this, never the validating constructor.
    /// </summary>
    public static GiftItemUrl Rehydrate(string value) => new(value, true);

    /// <summary>Non-throwing predicate for Application-ring validation (CONVENTIONS.md "Domain modelling", mirroring <see cref="GiftItemName.IsValidLength"/>). Null/blank is valid — Url is optional, and "must be http/https" cannot be a rule about the absence of a value.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxLength && IsAbsoluteHttpUrl(trimmed);
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public bool Equals(GiftItemUrl? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GiftItemUrl);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
