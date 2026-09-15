namespace GiftLists.Domain.GiftLists;

/// <summary>
/// Free-text notes about a gift item (CONVENTIONS.md "Domain modelling" — value object for anything with a rule;
/// GL-74). Optional, modelled the same way as <see cref="GiftItemUrl"/>: absence is a null
/// <see cref="GiftItem.Description"/> reference, not a "blank but valid" state.
/// </summary>
/// <remarks>
/// GL-74's decision on Description: bound it. It was a bare, unbounded <c>string?</c> written
/// into both this service's own Mongo document and the Gateway's read projection — an unbounded
/// write into two databases with no rule anywhere, which is exactly what CONVENTIONS.md "Domain modelling" means
/// by "anything with a rule" once a rule (a length bound) is decided on. <see cref="MaxLength"/>
/// is ten times <see cref="GiftItemName.MaxLength"/> (2000 vs. 200): a description is reasonably
/// longer prose than a name, but "reasonably longer" is not "unbounded".
/// </remarks>
public sealed class GiftItemDescription : IEquatable<GiftItemDescription>
{
    public const int MaxLength = 2000;

    /// <summary>Validates a description an owner is submitting right now.</summary>
    public GiftItemDescription(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Value must be at most {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    // Distinct arity from the validating constructor above purely to keep the two constructors
    // distinguishable by signature — see GiftItemUrl's matching constructor for the reasoning.
    private GiftItemDescription(string trustedValue, bool _) => Value = trustedValue;

    public string Value { get; }

    /// <summary>
    /// Rehydration-only, trusting storage rather than re-validating it — mirrors
    /// <see cref="GiftItemUrl.Rehydrate"/> and <see cref="ExpiryDate.Rehydrate"/>. The length
    /// bound is new with GL-74; a description already longer than <see cref="MaxLength"/> may
    /// already be persisted, and re-validating on every read would turn tightening the rule into
    /// an outage for that list. Only the Infrastructure persistence mapper calls this.
    /// </summary>
    public static GiftItemDescription Rehydrate(string value) => new(value, true);

    /// <summary>Non-throwing predicate for Application-ring validation (CONVENTIONS.md "Domain modelling", mirroring <see cref="GiftItemName.IsValidLength"/>). Null/blank is valid — Description is optional.</summary>
    public static bool IsValidLength(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= MaxLength;

    public bool Equals(GiftItemDescription? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GiftItemDescription);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
