namespace GiftLists.Domain.GiftLists;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") — keeps <c>AddItem(listId, itemId)</c>-shaped calls
/// from silently compiling with the two ids swapped.
/// </summary>
public readonly record struct GiftItemId(Guid Value)
{
    /// <summary>Client-generated, same rationale as <see cref="GiftListId.New"/>.</summary>
    public static GiftItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
