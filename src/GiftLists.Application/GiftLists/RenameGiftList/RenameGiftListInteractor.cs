using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.RenameGiftList;

/// <summary>
/// Assumes its request already passed <see cref="RenameGiftListValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// </summary>
internal sealed class RenameGiftListInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IRenameGiftList
{
    public async Task<Result<RenameGiftListResponse>> Handle(RenameGiftListRequest request, CancellationToken cancellationToken)
    {
        var list = await giftLists.FindByIdAsync(new GiftListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return GiftListErrors.NotFound;
        }

        // ARCHITECTURE.md "Auth & sharing": owner queries require JWT + ownership check. Done here, in the
        // interactor, rather than as a GiftList domain invariant — "who is allowed to call this"
        // is a caller-identity fact the aggregate itself never needs to reason about, unlike
        // "cannot add an item to an expired list" (a fact about the list's own state).
        if (list.OwnerId != new OwnerId(request.RequesterId))
        {
            return GiftListErrors.Forbidden;
        }

        var name = new GiftListName(request.Name);
        list.Rename(name, clock.UtcNow);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see CreateGiftListInteractor's own
        // comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new RenameGiftListResponse(list.Id.Value, list.Name.Value);
    }
}
