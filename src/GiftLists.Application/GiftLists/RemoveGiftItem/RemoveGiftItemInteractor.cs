using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.RemoveGiftItem;

/// <summary>
/// Assumes its request already passed <see cref="RemoveGiftItemValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// </summary>
internal sealed class RemoveGiftItemInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IRemoveGiftItem
{
    public async Task<Result<RemoveGiftItemResponse>> Handle(RemoveGiftItemRequest request, CancellationToken cancellationToken)
    {
        var list = await giftLists.FindByIdAsync(new GiftListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return GiftListErrors.NotFound;
        }

        if (list.OwnerId != new OwnerId(request.RequesterId))
        {
            return GiftListErrors.Forbidden;
        }

        var itemId = new GiftItemId(request.ItemId);

        // Checked here so a missing item is the expected, user-facing Result
        // (CONVENTIONS.md "Errors") — GiftList.RemoveItem's own throw is the backstop for a missed
        // check, not the primary path.
        if (list.Items.All(item => item.Id != itemId))
        {
            return GiftListErrors.ItemNotFound;
        }

        list.RemoveItem(itemId, clock.UtcNow);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see CreateGiftListInteractor's own
        // comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new RemoveGiftItemResponse();
    }
}
