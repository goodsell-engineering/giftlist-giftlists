using GiftLists.Application.Common;
using GiftLists.Domain.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// The GiftLists-specific implementation of the generic <see cref="IDomainEventPublisher"/>
/// (ARCHITECTURE.md "Domain events are not integration events"): maps each domain event to its integration event and publishes it.
/// </summary>
/// <remarks>
/// Publishing is synchronous, straight after the Mongo write, with no outbox — a known, accepted
/// dual-write risk (ARCHITECTURE.md "Event publishing: synchronous"): if the write succeeds and this then fails, the read
/// model drifts from GiftLists' own data with no automatic repair. Given that risk is already
/// accepted, rethrowing from here would only make things worse — it would land this already-
/// applied write in Rebus's error queue for a retry that re-runs a mutation which already
/// happened, and (for the fire-and-forget commands every use case in this service is, per
/// ARCHITECTURE.md "Command → event flow") it would fail a caller who has no yes/no to wait on anyway. Instead this
/// swallows the failure and logs it at <see cref="LogLevel.Critical"/>, deliberately loud, so the
/// drift is a page an operator sees rather than a silent gap discovered weeks later. Recovery is
/// manual (restart/replay), exactly as ARCHITECTURE.md "Event publishing: synchronous" anticipates.
/// </remarks>
internal sealed class GiftListEventPublisher(IBus bus, ILogger<GiftListEventPublisher> logger) : IDomainEventPublisher
{
    public async Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        // The try wraps the WHOLE loop, not each iteration. Per-iteration, a failure on event 1 of
        // N logged and then published 2..N, emitting a partial sequence with a hole in it — a
        // strictly worse state to reconcile from than "nothing after the failure went", because a
        // consumer cannot tell a gap from a not-yet-arrived event. Stopping keeps the drift
        // contiguous and describable (Batch 11 review).
        var published = 0;

        try
        {
            foreach (var domainEvent in domainEvents)
            {
                await bus.Publish(GiftListEventMapper.ToIntegrationEvent(domainEvent));
                published++;
            }
        }
        catch (Exception ex)
        {
            // NOTHING IN THIS BLOCK MAY CALL ToIntegrationEvent. It used to log that call's result
            // for the failed event, which meant that when the caught exception WAS the mapper's
            // default-arm throw — an unmapped domain event, the one case GL-21's exhaustiveness
            // guard exists for — the catch re-invoked the same throwing call, the exception escaped
            // PublishAsync, and no Critical log was written at all. The class's entire argument for
            // swallowing rather than rethrowing was defeated in precisely the scenario it describes.
            // Introduced by me in Batch 11 and found by the GL-21 agent. The V1 type name is
            // derivable from the domain event type name, so logging the latter loses nothing;
            // ListIdOf is non-throwing by design for the same reason.
            var failed = domainEvents.ElementAt(published);

            logger.LogCritical(
                ex,
                "Failed to publish the integration event for domain event {DomainEventType} for " +
                "gift list {ListId}; {PublishedCount} of {TotalCount} event(s) in this batch were " +
                "published and the rest were abandoned. The write they describe already succeeded, so " +
                "GiftLists' read-model consumers (Gateway, Reservation) are now missing them until this " +
                "is manually reconciled (ARCHITECTURE.md \"Event publishing: synchronous\", dual-write risk accepted knowingly). " +
                "The list id is here because it is the only way to know WHICH list drifted — this log " +
                "is the sole record of that, and it is not PII.",
                failed.GetType().Name,
                GiftListEventMapper.ListIdOf(failed),
                published,
                domainEvents.Count);
        }
    }
}
