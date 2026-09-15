using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.RemoveGiftItem;

/// <summary>The named input port for "remove an item from a gift list" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IRemoveGiftItem : IInteractor<RemoveGiftItemRequest, RemoveGiftItemResponse>;
