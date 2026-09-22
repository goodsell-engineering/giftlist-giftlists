using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftListExpiry;

/// <summary>
/// Assumes its request already passed <see cref="ChangeGiftListExpiryValidator"/> — validation
/// is the composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// Deliberately does not refuse an already-expired list: the only rule is that the NEW expiry is
/// in the future (<see cref="GiftList.ChangeExpiry"/>'s own doc comment).
/// </summary>
internal sealed class ChangeGiftListExpiryInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IChangeGiftListExpiry
{
    public async Task<Result<ChangeGiftListExpiryResponse>> Handle(ChangeGiftListExpiryRequest request, CancellationToken cancellationToken)
    {
        var list = await giftLists.FindByIdAsync(new GiftListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return GiftListErrors.NotFound;
        }

        // Ownership is checked here, not in the aggregate — see RenameGiftListInteractor's own
        // comment for why.
        if (list.OwnerId != new OwnerId(request.RequesterId))
        {
            return GiftListErrors.Forbidden;
        }

        var now = clock.UtcNow;
        var expiry = new ExpiryDate(request.ExpiresAt, now);

        // Redelivery guard (CONVENTIONS.md "Messaging" — handlers must be safe to run more than
        // once): a second delivery of the same command finds the expiry already where it asked
        // for it, and must not raise a second GiftListExpiryChanged — the saga would reschedule
        // against an identical instant, and every consumer would replay a change that changed
        // nothing. Mirrors AddGiftItemInteractor's "already holds this itemId" early return.
        // Equals, not ==: ExpiryDate is a class with value equality but no operator overload,
        // so == would be reference equality and this guard would never fire.
        if (list.Expiry.Equals(expiry))
        {
            return new ChangeGiftListExpiryResponse(list.Id.Value, list.Expiry.Value);
        }

        list.ChangeExpiry(expiry, now);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see
        // CreateGiftListInteractor's own comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new ChangeGiftListExpiryResponse(list.Id.Value, list.Expiry.Value);
    }
}
