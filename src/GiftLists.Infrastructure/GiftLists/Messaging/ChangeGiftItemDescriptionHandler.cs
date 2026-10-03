using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.ChangeGiftItemDescription;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class ChangeGiftItemDescriptionHandler(
    IInteractor<ChangeGiftItemDescriptionRequest, ChangeGiftItemDescriptionResponse> changeGiftItemDescription)
    : IHandleMessages<ChangeGiftItemDescription>
{
    public async Task Handle(ChangeGiftItemDescription message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new ChangeGiftItemDescriptionRequest(
            message.ListId, message.RequesterId, message.ItemId, message.Description);
        await changeGiftItemDescription.Handle(request, cancellationToken);
    }
}
