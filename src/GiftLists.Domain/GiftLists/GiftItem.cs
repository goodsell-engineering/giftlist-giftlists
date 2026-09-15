namespace GiftLists.Domain.GiftLists;

/// <summary>
/// A single wish on a <see cref="GiftList"/>. Not an aggregate root of its own — it is reached
/// only through <see cref="GiftList"/> (CONVENTIONS.md "Persistence" — no <c>IGiftItemRepository</c>) and
/// raises no domain events itself; <see cref="GiftList.AddItem"/>/<see cref="GiftList.RemoveItem"/>
/// raise <c>GiftItemAdded</c>/<c>GiftItemRemoved</c> on its behalf, the same way every other
/// mutation on this aggregate is recorded on the root.
/// </summary>
public sealed class GiftItem
{
    private GiftItem(GiftItemId id, GiftItemName name, GiftItemDescription? description, GiftItemUrl? url)
    {
        Id = id;
        Name = name;
        Description = description;
        Url = url;
    }

    public GiftItemId Id { get; }

    public GiftItemName Name { get; }

    public GiftItemDescription? Description { get; }

    public GiftItemUrl? Url { get; }

    /// <summary>
    /// The only way to construct a <see cref="GiftItem"/> — used both by
    /// <see cref="GiftList.AddItem"/> (a genuinely new item, same assembly) and by the
    /// Infrastructure persistence mapper (rehydrating one out of storage, cross-assembly). One
    /// factory rather than a Create/Rehydrate pair like <see cref="GiftList"/>'s: unlike the
    /// aggregate root, this entity never raises a domain event on its own construction, so there
    /// is no "new" vs. "reloaded" behavioural difference to keep apart (CONVENTIONS.md "Domain modelling" — no
    /// public parameterless constructor; rehydration goes through the mapper). GL-74 moved the
    /// "new" vs. "reloaded" distinction for <c>description</c>/<c>url</c> one level down, into
    /// <see cref="GiftItemDescription"/>/<see cref="GiftItemUrl"/> themselves (each type's own
    /// doc comment has the reasoning) — the caller passes an already-constructed value object
    /// either way, so this factory still needs exactly one path.
    /// </summary>
    public static GiftItem Create(GiftItemId id, GiftItemName name, GiftItemDescription? description, GiftItemUrl? url) =>
        new(id, name, description, url);
}
