namespace GiftLists.Domain.GiftLists;

/// <summary>
/// Strongly-typed id (CONVENTIONS.md "Domain modelling") — a bare <see cref="Guid"/> would let a caller pass a
/// gift-item id or any other GUID-shaped value where a gift-list id belongs and the compiler
/// would never notice.
/// </summary>
public readonly record struct GiftListId(Guid Value)
{
    /// <summary>
    /// Gift-list ids are client-generated (ARCHITECTURE.md "Command → event flow" — the Gateway sends
    /// <c>CreateGiftList</c> with a client-generated id so the SPA can navigate to the new list
    /// before the read model catches up), so this wraps an id the caller already has rather than
    /// minting one — unlike <c>UserId.New()</c> in Identity, which mints server-side.
    /// </summary>
    public static GiftListId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
