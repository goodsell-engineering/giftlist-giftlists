namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>The embedded shape of a gift item within <see cref="GiftListDocument.Items"/> (ARCHITECTURE.md "Data model": <c>items: [{ itemId, name, description, url, priority }]</c>). No reservation field — that would be structurally impossible here by design; reservation state lives entirely in the Reservation service.</summary>
public sealed class GiftItemDocument
{
    public required Guid ItemId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Url { get; init; }

    /// <summary>
    /// Nullable on purpose: items stored before GL-146 have no <c>priority</c> field, and a
    /// non-nullable <c>int</c> would read an absent field as 0 (which the domain rejects), not as
    /// "missing". <see cref="GiftListToDocumentMapper.ToAggregate"/> defaults null to 3. Always
    /// written for new items.
    /// </summary>
    public int? Priority { get; init; }
}
