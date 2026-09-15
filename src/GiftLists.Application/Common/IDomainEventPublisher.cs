using GiftLists.Domain.Common;

namespace GiftLists.Application.Common;

/// <summary>
/// Publishes an aggregate's saved domain events as integration events (ARCHITECTURE.md "Domain events are not integration events").
/// Kept separate from a repository's write methods deliberately — a repository that both writes
/// and publishes conflates persistence with messaging. Genuinely domain-agnostic in signature
/// (it never mentions <c>GiftList</c> specifically), so it lives in <c>Common/</c> next to
/// <see cref="IClock"/> rather than beside
/// <c>GiftLists.Application.GiftLists.IGiftListRepository</c> — even though, with GiftLists'
/// one aggregate, its only implementation today is GiftLists-specific (mirrors
/// <c>Identity.Application.Common.IDomainEventPublisher</c>, GL-16).
///
/// Every interactor calls this only after its repository write has already succeeded — save
/// first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — and the mapping from domain to integration
/// event happens behind this port, in Infrastructure, never in Application (ARCHITECTURE.md "Domain events are not integration events").
/// Publishing is synchronous with no outbox: a dual-write risk accepted knowingly for this demo
/// (ARCHITECTURE.md "Event publishing: synchronous"). The Infrastructure implementation is expected to swallow a publish
/// failure rather than let it fail an already-successful write, logging it loudly (Critical)
/// instead — see that implementation's own doc comment for the reasoning.
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
