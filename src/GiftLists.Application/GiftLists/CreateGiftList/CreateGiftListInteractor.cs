using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.CreateGiftList;

/// <summary>
/// Assumes its request already passed <see cref="CreateGiftListValidator"/> — validation is the
/// composition root's decorator's job (CONVENTIONS.md "Use cases"), not this interactor's.
/// </summary>
internal sealed class CreateGiftListInteractor(
    IGiftListRepository giftLists,
    IDomainEventPublisher giftListEvents,
    IShareTokenGenerator shareTokens,
    IClock clock) : ICreateGiftList
{
    public async Task<Result<CreateGiftListResponse>> Handle(CreateGiftListRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var name = new GiftListName(request.Name);
        var expiry = new ExpiryDate(request.ExpiresAt, now);
        var shareToken = new ShareToken(shareTokens.Generate());

        var list = GiftList.Create(
            new GiftListId(request.ListId),
            new OwnerId(request.OwnerId),
            name,
            expiry,
            shareToken,
            now);

        var saved = await giftLists.AddAsync(list, cancellationToken);
        if (saved.IsFailure)
        {
            return Result<CreateGiftListResponse>.Failure(saved.Error);
        }

        // Save first, publish second (ARCHITECTURE.md "Event publishing: synchronous") — a published event describing a
        // write that didn't happen is worse than a write nobody heard about. The two are not
        // atomic: this is the dual-write risk ARCHITECTURE.md "Event publishing: synchronous" accepts knowingly for this demo (no outbox);
        // see GiftListEventPublisher for how a publish failure is handled (logged loudly, not
        // thrown) so it never masks the write that already succeeded.
        await giftListEvents.PublishAsync(list.DomainEvents, cancellationToken);
        list.ClearDomainEvents();

        return new CreateGiftListResponse(list.Id.Value, list.ShareToken.Value, list.Expiry.Value);
    }
}
