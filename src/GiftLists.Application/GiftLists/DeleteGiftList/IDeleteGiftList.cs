using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.DeleteGiftList;

/// <summary>The named input port for "delete a gift list" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IDeleteGiftList : IInteractor<DeleteGiftListRequest, DeleteGiftListResponse>;
