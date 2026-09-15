namespace GiftLists.Domain.GiftLists;

/// <summary>The display name of a gift list. A value object purely for its length rule (CONVENTIONS.md "Domain modelling").</summary>
public sealed class GiftListName : IEquatable<GiftListName>
{
    private const int MaxLength = 100;

    public GiftListName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Value must be at most {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public string Value { get; }

    /// <summary>
    /// The same rule the constructor enforces, exposed as a non-throwing predicate so an
    /// Application-ring validator (CONVENTIONS.md "Use cases") can reject bad input as a
    /// <c>Result</c> before it ever reaches the constructor — see
    /// <c>Identity.Domain.Users.DisplayName.IsValidLength</c> for the pattern this mirrors.
    /// </summary>
    public static bool IsValidLength(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaxLength;

    public bool Equals(GiftListName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GiftListName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
