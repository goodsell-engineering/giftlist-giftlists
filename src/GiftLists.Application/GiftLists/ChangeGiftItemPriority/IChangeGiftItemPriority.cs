using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.ChangeGiftItemPriority;

/// <summary>The named input port for "change an item's priority" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IChangeGiftItemPriority : IInteractor<ChangeGiftItemPriorityRequest, ChangeGiftItemPriorityResponse>;
