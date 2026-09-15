namespace GiftLists.Infrastructure.GiftLists.Persistence;

/// <summary>The embedded shape of a gift item within <see cref="GiftListDocument.Items"/> (ARCHITECTURE.md "Data model": <c>items: [{ itemId, name, description, url }]</c>). No reservation field — that would be structurally impossible here by design; reservation state lives entirely in the Reservation service.</summary>
public sealed class GiftItemDocument
{
    public required Guid ItemId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Url { get; init; }
}
