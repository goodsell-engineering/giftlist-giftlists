using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftItemDescription;

/// <summary>
/// Assumes its request already passed <see cref="ChangeGiftItemDescriptionValidator"/> —
/// validation is the composition root's decorator's job (CONVENTIONS.md "Use cases"), not this
/// interactor's. Check order (not_found → forbidden → item_not_found → expired → no-op) is a
/// plan decision (D2): it follows the codebase's existing checks
/// (<see cref="Application.GiftLists.RemoveGiftItem.RemoveGiftItemInteractor"/>,
/// <see cref="Application.GiftLists.AddGiftItem.AddGiftItemInteractor"/>), not the design
/// document's flow narrative, and nobody outside this service can see the difference (GL-72).
/// </summary>
internal sealed class ChangeGiftItemDescriptionInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IChangeGiftItemDescription
{
    public async Task<Result<ChangeGiftItemDescriptionResponse>> Handle(
        ChangeGiftItemDescriptionRequest request, CancellationToken cancellationToken)
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

        // Blank/whitespace-only normalises to "absent", matching AddGiftItemInteractor — the
        // request already passed ChangeGiftItemDescriptionValidator, so a present value is
        // known-valid.
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : new GiftItemDescription(request.Description);

        // Redelivery / same-value guard (D5's "no-op" case, CONVENTIONS.md "Messaging"):
        // compared null-safely and by value (GiftItemDescription.Equals), so
        // "  Size M  " against a stored "Size M" is a no-op too. Nothing is saved or published.
        if (Equals(item.Description, description))
        {
            return new ChangeGiftItemDescriptionResponse();
        }

        list.ChangeItemDescription(itemId, description, now);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see
        // CreateGiftListInteractor's own comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new ChangeGiftItemDescriptionResponse();
    }
}
