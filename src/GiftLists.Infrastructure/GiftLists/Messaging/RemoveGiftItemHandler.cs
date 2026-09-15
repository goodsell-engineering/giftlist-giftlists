using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.RemoveGiftItem;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class RemoveGiftItemHandler(IInteractor<RemoveGiftItemRequest, RemoveGiftItemResponse> removeGiftItem)
    : IHandleMessages<RemoveGiftItem>
{
    public async Task Handle(RemoveGiftItem message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new RemoveGiftItemRequest(message.ListId, message.RequesterId, message.ItemId);
        await removeGiftItem.Handle(request, cancellationToken);
    }
}
