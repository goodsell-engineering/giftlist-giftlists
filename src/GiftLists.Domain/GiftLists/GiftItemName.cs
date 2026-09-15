namespace GiftLists.Domain.GiftLists;

/// <summary>The display name of a gift item. A distinct value object from <see cref="GiftListName"/> — same shape, different owning concept — so a list name can never be passed where an item name belongs (CONVENTIONS.md "Domain modelling").</summary>
public sealed class GiftItemName : IEquatable<GiftItemName>
{
    private const int MaxLength = 200;

    public GiftItemName(string value)
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

    /// <summary>Non-throwing predicate for Application-ring validation — see <see cref="GiftListName.IsValidLength"/>.</summary>
    public static bool IsValidLength(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= MaxLength;

    public bool Equals(GiftItemName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GiftItemName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
