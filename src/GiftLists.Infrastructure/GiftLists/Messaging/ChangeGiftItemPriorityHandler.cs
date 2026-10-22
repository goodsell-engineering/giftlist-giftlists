using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.ChangeGiftItemPriority;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class ChangeGiftItemPriorityHandler(
    IInteractor<ChangeGiftItemPriorityRequest, ChangeGiftItemPriorityResponse> changeGiftItemPriority)
    : IHandleMessages<ChangeGiftItemPriority>
{
    public async Task Handle(ChangeGiftItemPriority message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new ChangeGiftItemPriorityRequest(
            message.ListId, message.RequesterId, message.ItemId, message.Priority);
        await changeGiftItemPriority.Handle(request, cancellationToken);
    }
}
