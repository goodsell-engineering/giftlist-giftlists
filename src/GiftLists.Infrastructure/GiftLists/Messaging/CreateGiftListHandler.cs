using GiftLists.Application.Common;
using GiftLists.Application.GiftLists.CreateGiftList;
using GiftLists.Contracts.GiftLists;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace GiftLists.Infrastructure.GiftLists.Messaging;

/// <summary>
/// Thin by design (CONVENTIONS.md "Messaging" — no business logic in a handler): translate the wire
/// command into <see cref="CreateGiftListRequest"/> and call the one input port. Fire-and-forget
/// (ARCHITECTURE.md "Command → event flow" — no <c>bus.Reply</c>, unlike Identity's SignUp/Login): nobody is
/// waiting synchronously, so there is nothing to translate the <c>Result</c> back into. Its
/// success or failure was already recorded by the <c>Logging&lt;,&gt;</c> decorator, and the SPA
/// learns the outcome once the read model catches up with <c>GiftListCreatedV1</c>.
/// </summary>
internal sealed class CreateGiftListHandler(IInteractor<CreateGiftListRequest, CreateGiftListResponse> createGiftList)
    : IHandleMessages<CreateGiftList>
{
    public async Task Handle(CreateGiftList message)
    {
        var cancellationToken = MessageContext.Current.GetCancellationToken();
        var request = new CreateGiftListRequest(message.ListId, message.OwnerId, message.Name, message.ExpiresAt);
        await createGiftList.Handle(request, cancellationToken);
    }
}
