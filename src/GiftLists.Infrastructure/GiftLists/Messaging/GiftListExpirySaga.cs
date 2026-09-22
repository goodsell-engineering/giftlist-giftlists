using GiftLists.Application.Common;
using GiftLists.Contracts.GiftLists.Events;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// List expiry as a Rebus saga, exactly as ARCHITECTURE.md "Sagas: list expiry" lays it out:
/// <c>GiftListCreated</c> starts it and defers a timeout to <c>expiresAt</c>; an expiry change
/// reschedules; the timeout publishes <see cref="GiftListExpiredV1"/>; deleting the list ends it.
/// Never a Mongo TTL index — that deletes the document, and an expired list must stay visible
/// (ARCHITECTURE.md "Auth & sharing").
/// </summary>
/// <remarks>
/// <para>
/// <b>Driven by this service's own integration events</b>, consumed off the same <c>giftlist</c>
/// queue Gateway and Reservation read them from — the composition root subscribes to the three it
/// handles (and only those: an event on the queue with no handler is a dispatch failure, not a
/// no-op). That is why the saga is keyed on <c>*V1</c> types and not on the domain events: it sits
/// in Infrastructure, downstream of the same publish every other consumer sees, so it can never
/// observe a list that was not saved and published first (ARCHITECTURE.md "Event publishing:
/// synchronous").
/// </para>
/// <para>
/// <b>Not a handler in the CONVENTIONS.md "Messaging" sense.</b> A handler translates a message
/// and calls one input port; this calls none, because expiry mutates nothing — it is a query-time
/// predicate on the list's own <c>expiresAt</c> and on every projection of it. The only
/// state here is Rebus's own (<see cref="GiftListExpirySagaData"/>), and the only output is a
/// notification that an instant passed. The little logic it does hold — "is this timeout the
/// one I am waiting for" — is the saga's, not a use case's.
/// </para>
/// <para>
/// <b>Rescheduling never cancels.</b> Rebus has no way to withdraw a deferred message, so every
/// timeout ever scheduled arrives. <see cref="GiftListExpiryDue"/> carries the expiry it was
/// scheduled for and <see cref="GiftListExpirySagaData.ExpiresAtUtc"/> holds the current one;
/// only a match publishes. Move an expiry from T1 to T2 and the T1 timeout arrives, is
/// recognised as superseded, and is dropped.
/// </para>
/// <para>
/// <b>Redelivery and ordering are handled by the same two comparisons.</b> Delivery is
/// at-least-once (CONVENTIONS.md "Messaging"). A redelivered <see cref="GiftListCreatedV1"/>
/// finds the saga already started and does nothing; a redelivered
/// <see cref="GiftListExpiryChangedV1"/> finds the expiry already where it says and does nothing.
/// An expiry change that overtakes its own creation event starts the saga itself — which is also
/// what lets an owner revive a list whose saga already fired — and the late creation event is
/// then the no-op. A timeout or deletion for a saga that has already ended is logged by Rebus as
/// "no saga found" and dropped; both are ordinary (delete an expired list, or delete before
/// expiry and let the timeout come due), not faults.
/// </para>
/// <para>
/// <b>One thing it deliberately does not do:</b> check that the list still exists before
/// publishing. A deletion that overtakes its own creation on the queue would leave a saga that
/// eventually announces the expiry of a list that is gone. That needs the two events to be
/// reordered within the seconds between an owner creating a list and deleting it, and the cost
/// is one <see cref="GiftListExpiredV1"/> for an id no consumer holds — which every consumer must
/// already tolerate, since no consumer may gate anything on this event arriving
/// (ARCHITECTURE.md "Auth & sharing"). Loading the aggregate here to close that would put a
/// repository into a message handler for a window nobody can hit by hand.
/// </para>
/// </remarks>
internal sealed class GiftListExpirySaga(IBus bus, IClock clock) : Saga<GiftListExpirySagaData>,
    IAmInitiatedBy<GiftListCreatedV1>,
    IAmInitiatedBy<GiftListExpiryChangedV1>,
    IHandleMessages<GiftListDeletedV1>,
    IHandleMessages<GiftListExpiryDue>
{
    protected override void CorrelateMessages(ICorrelationConfig<GiftListExpirySagaData> config)
    {
        config.Correlate<GiftListCreatedV1>(m => m.ListId, d => d.ListId);
        config.Correlate<GiftListExpiryChangedV1>(m => m.ListId, d => d.ListId);
        config.Correlate<GiftListDeletedV1>(m => m.ListId, d => d.ListId);
        config.Correlate<GiftListExpiryDue>(m => m.ListId, d => d.ListId);
    }

    public Task Handle(GiftListCreatedV1 message)
    {
        // Not new means either a redelivery or an expiry change that got here first; in both
        // cases what the saga holds is at least as current as this message.
        if (!IsNew)
        {
            return Task.CompletedTask;
        }

        return ScheduleAsync(message.ListId, message.ExpiresAt);
    }

    public Task Handle(GiftListExpiryChangedV1 message)
    {
        if (!IsNew && Data.ExpiresAtUtc == message.ExpiresAt.UtcDateTime)
        {
            return Task.CompletedTask;
        }

        return ScheduleAsync(message.ListId, message.ExpiresAt);
    }

    public Task Handle(GiftListDeletedV1 message)
    {
        MarkAsComplete();
        return Task.CompletedTask;
    }

    public async Task Handle(GiftListExpiryDue message)
    {
        if (message.ExpiresAt.UtcDateTime != Data.ExpiresAtUtc)
        {
            return;
        }

        await bus.Publish(new GiftListExpiredV1(message.ListId, message.ExpiresAt));
        MarkAsComplete();
    }

    private async Task ScheduleAsync(Guid listId, DateTimeOffset expiresAt)
    {
        Data.ListId = listId;
        Data.ExpiresAtUtc = expiresAt.UtcDateTime;

        // An expiry already in the past (a creation event delivered late, or after a backlog) is
        // deferred by nothing at all rather than by a negative span: the timeout is then due on
        // the store's next poll, and the list is announced expired as soon as this service learns
        // of it. DeferLocal, not Defer — the timeout returns to this bus's own queue, so it needs
        // no route (CONVENTIONS.md "Persistence": a route written against the wrong queue name
        // fails quietly).
        var delay = expiresAt - clock.UtcNow;
        await bus.DeferLocal(
            delay < TimeSpan.Zero ? TimeSpan.Zero : delay,
            new GiftListExpiryDue(listId, expiresAt));
    }
}
