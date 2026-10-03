using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.ChangeGiftItemDescription;

/// <summary>The named input port for "change or clear an item's description" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IChangeGiftItemDescription : IInteractor<ChangeGiftItemDescriptionRequest, ChangeGiftItemDescriptionResponse>;
