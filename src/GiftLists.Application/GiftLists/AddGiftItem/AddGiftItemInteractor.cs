using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.AddGiftItem;

/// <summary>
/// Assumes its request already passed <see cref="AddGiftItemValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// </summary>
internal sealed class AddGiftItemInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IAddGiftItem
{
    public async Task<Result<AddGiftItemResponse>> Handle(AddGiftItemRequest request, CancellationToken cancellationToken)
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

        var now = clock.UtcNow;

        // "you cannot add an item to an expired list" is a domain invariant, not a UI check
        // (CONVENTIONS.md "Domain modelling"). Checked here so the expected, user-facing outcome
        // is a Result (CONVENTIONS.md "Errors"), with GiftList.AddItem's own check as the
        // backstop for a missed one (that type's own doc comment has the detail).
        if (list.Expiry.HasExpired(now))
        {
            return GiftListErrors.Expired;
        }

        var name = new GiftItemName(request.Name);
        var itemId = new GiftItemId(request.ItemId);
        // Constructed here, not left as bare strings — request already passed
        // AddGiftItemValidator (this interactor's own doc comment), so these are known-valid.
        // Blank/whitespace-only is normalised to "absent" BEFORE construction, matching
        // GiftItemDescription.IsValidLength/GiftItemUrl.IsValid, which accept blank as valid for
        // exactly that reason — both constructors reject blank outright (they validate a
        // PRESENT value), so passing one through unnormalised would let a validator-approved
        // request throw here instead of returning the Result the design calls for.
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : new GiftItemDescription(request.Description);
        var url = string.IsNullOrWhiteSpace(request.Url)
            ? null
            : new GiftItemUrl(request.Url);

        // Re-processing safety. Delivery is at-least-once and GL-54 was closed without the shared
        // processedMessages inbox, so a handler being safe to run twice is the ONLY line of defence
        // rather than a second one. ItemId is client-generated and stable across redeliveries, so a
        // redelivered AddGiftItem must be a no-op returning the same answer — without this,
        // GiftList.AddItem appends unconditionally and one list ends up holding the same item twice
        // (demonstrated in the Batch 11 review). This is the one handler of the five that could
        // corrupt state on redelivery; the other four already fail benignly.
        if (list.Items.Any(existing => existing.Id == itemId))
        {
            return new AddGiftItemResponse(itemId.Value);
        }

        list.AddItem(itemId, name, description, url, now);

        await giftLists.UpdateAsync(list, cancellationToken);

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see CreateGiftListInteractor's own
        // comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new AddGiftItemResponse(itemId.Value);
    }
}
