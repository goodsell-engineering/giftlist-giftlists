using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.AddGiftItem;

/// <summary>The named input port for "add an item to a gift list" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IAddGiftItem : IInteractor<AddGiftItemRequest, AddGiftItemResponse>;
