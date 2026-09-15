using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.RenameGiftList;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class RenameGiftListHandler(IInteractor<RenameGiftListRequest, RenameGiftListResponse> renameGiftList)
    : IHandleMessages<RenameGiftList>
{
    public async Task Handle(RenameGiftList message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new RenameGiftListRequest(message.ListId, message.RequesterId, message.Name);
        await renameGiftList.Handle(request, cancellationToken);
    }
}
