using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftItemPriority;

/// <summary>
/// Assumes its request already passed <see cref="ChangeGiftItemPriorityValidator"/> — validation
/// is the composition root's decorator's job (CONVENTIONS.md "Use cases"). Check order (not_found
/// → forbidden → item_not_found → expired → no-op) is the same as
/// <see cref="Application.GiftLists.ChangeGiftItemDescription.ChangeGiftItemDescriptionInteractor"/>'s.
/// </summary>
internal sealed class ChangeGiftItemPriorityInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IChangeGiftItemPriority
{
    public async Task<Result<ChangeGiftItemPriorityResponse>> Handle(
        ChangeGiftItemPriorityRequest request, CancellationToken cancellationToken)
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
        var item = list.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return GiftListErrors.ItemNotFound;
        }

        var now = clock.UtcNow;
        if (list.Expiry.HasExpired(now))
        {
            return GiftListErrors.Expired;
        }

        // Redelivery / same-value guard: compared by value on the resolved priority, so 3 against
        // an item stored with no priority (read as 3) is a no-op too. Nothing is saved or published.
        var priority = new GiftItemPriority(request.Priority);
        if (item.Priority.Equals(priority))
        {
            return new ChangeGiftItemPriorityResponse();
        }

        list.ChangeItemPriority(itemId, priority, now);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous").
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new ChangeGiftItemPriorityResponse();
    }
}
