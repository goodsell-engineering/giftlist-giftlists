using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.AddGiftItem;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class AddGiftItemHandler(IInteractor<AddGiftItemRequest, AddGiftItemResponse> addGiftItem)
    : IHandleMessages<AddGiftItem>
{
    public async Task Handle(AddGiftItem message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new AddGiftItemRequest(
            message.ListId, message.RequesterId, message.ItemId, message.Name, message.Description, message.Url);
        await addGiftItem.Handle(request, cancellationToken);
    }
}
