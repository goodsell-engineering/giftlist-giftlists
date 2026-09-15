using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.DeleteGiftList;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>Thin by design — see <see cref="CreateGiftListHandler"/> for the rationale.</summary>
internal sealed class DeleteGiftListHandler(IInteractor<DeleteGiftListRequest, DeleteGiftListResponse> deleteGiftList)
    : IHandleMessages<DeleteGiftList>
{
    public async Task Handle(DeleteGiftList message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new DeleteGiftListRequest(message.ListId, message.RequesterId);
        await deleteGiftList.Handle(request, cancellationToken);
    }
}
