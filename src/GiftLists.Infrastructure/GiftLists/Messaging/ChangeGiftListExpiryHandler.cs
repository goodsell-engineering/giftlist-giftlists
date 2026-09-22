using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.ChangeGiftListExpiry;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class ChangeGiftListExpiryHandler(IInteractor<ChangeGiftListExpiryRequest, ChangeGiftListExpiryResponse> changeGiftListExpiry)
    : IHandleMessages<ChangeGiftListExpiry>
{
    public async Task Handle(ChangeGiftListExpiry message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new ChangeGiftListExpiryRequest(message.ListId, message.RequesterId, message.ExpiresAt);
        await changeGiftListExpiry.Handle(request, cancellationToken);
    }
}
