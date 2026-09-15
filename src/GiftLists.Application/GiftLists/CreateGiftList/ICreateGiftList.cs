using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.CreateGiftList;

/// <summary>
/// The named input port for "create a gift list" (CONVENTIONS.md "Naming"). Declared for the naming
/// convention and for readability on <see cref="CreateGiftListInteractor"/>'s own base list — see
/// <see cref="IInteractor{TRequest,TResponse}"/>'s doc comment for why nothing resolves this
/// specific type from the container.
/// </summary>
public interface ICreateGiftList : IInteractor<CreateGiftListRequest, CreateGiftListResponse>;
