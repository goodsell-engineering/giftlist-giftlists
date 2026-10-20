namespace GiftLists.Domain.GiftLists;

/// <summary>
/// How much the owner wants an item: 1 to 5, 5 the most wanted (GL-146). Never optional on a
/// <see cref="GiftItem"/>: an item with no stated priority holds <see cref="Default"/>, so every
/// reader sees a value. Items stored before priorities existed have no stored value; the
/// persistence mapper reads them as <see cref="Default"/>.
/// </summary>
public sealed class GiftItemPriority : IEquatable<GiftItemPriority>
{
    public const int Min = 1;
    public const int Max = 5;
    private const int DefaultValue = 3;

    public GiftItemPriority(int value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between {Min} and {Max}.");
        }

        Value = value;
    }

    /// <summary>What an item holds when the owner states no priority, and what a stored item with none reads as.</summary>
    public static GiftItemPriority Default { get; } = new(DefaultValue);

    public int Value { get; }

    /// <summary>Non-throwing predicate for Application-ring validation (mirrors <see cref="GiftItemName.IsValidLength"/>).</summary>
    public static bool IsValid(int value) => value is >= Min and <= Max;

    public bool Equals(GiftItemPriority? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GiftItemPriority);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
