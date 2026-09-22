using GiftLists.Contracts.GiftLists.Events;
using GiftLists.Domain.Common;
using GiftLists.Domain.GiftLists.Events;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// Translates domain events into the integration events GiftLists actually publishes
/// (ARCHITECTURE.md "Domain events are not integration events" — and they never leave the process
/// directly).
/// </summary>
internal static class GiftListEventMapper
{
    public static object ToIntegrationEvent(IDomainEvent domainEvent) => domainEvent switch
    {
        GiftListCreated e => new GiftListCreatedV1(
            e.ListId.Value, e.OwnerId.Value, e.Name.Value, e.Expiry.Value, e.ShareToken.Value, e.CreatedAt),
        GiftListRenamed e => new GiftListRenamedV1(e.ListId.Value, e.Name.Value, e.RenamedAt),
        GiftListExpiryChanged e => new GiftListExpiryChangedV1(e.ListId.Value, e.Expiry.Value, e.ChangedAt),
        GiftListDeleted e => new GiftListDeletedV1(e.ListId.Value, e.DeletedAt),
        GiftItemAdded e => new GiftItemAddedV1(
            e.ListId.Value, e.ItemId.Value, e.Name.Value, e.Description?.Value, e.Url?.Value, e.AddedAt),
        GiftItemRemoved e => new GiftItemRemovedV1(e.ListId.Value, e.ItemId.Value, e.RemovedAt),
        _ => throw new InvalidOperationException(
            $"No integration event mapping for domain event '{domainEvent.GetType().Name}'."),
    };

    /// <summary>
    /// The id of the list an event concerns, for diagnostics. Lives here, beside the exhaustive
    /// mapping switch, so per-event-type knowledge stays in one place rather than being
    /// re-derived by a caller that only needs one field.
    /// </summary>
    public static Guid ListIdOf(IDomainEvent domainEvent) => domainEvent switch
    {
        GiftListCreated e => e.ListId.Value,
        GiftListRenamed e => e.ListId.Value,
        GiftListExpiryChanged e => e.ListId.Value,
        GiftListDeleted e => e.ListId.Value,
        GiftItemAdded e => e.ListId.Value,
        GiftItemRemoved e => e.ListId.Value,
        _ => Guid.Empty,
    };
}
