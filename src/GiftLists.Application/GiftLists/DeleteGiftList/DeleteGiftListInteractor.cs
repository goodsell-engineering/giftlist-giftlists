using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.DeleteGiftList;

/// <summary>
/// Assumes its request already passed <see cref="DeleteGiftListValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// </summary>
internal sealed class DeleteGiftListInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IClock clock) : IDeleteGiftList
{
    public async Task<Result<DeleteGiftListResponse>> Handle(DeleteGiftListRequest request, CancellationToken cancellationToken)
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

        // Raises GiftListDeleted locally; no persisted state to update here — the document is
        // removed outright below rather than flagged deleted (GiftList.Delete's own comment).
        list.Delete(clock.UtcNow);

        await giftLists.DeleteAsync(list.Id, cancellationToken);

        // Save (here, delete) first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — see
        // CreateGiftListInteractor's own comment for the dual-write trade-off this accepts.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new DeleteGiftListResponse();
    }
}
