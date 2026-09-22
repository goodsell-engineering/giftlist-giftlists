using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.ChangeGiftListExpiry;

/// <summary>The named input port for "move a gift list's expiry" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IChangeGiftListExpiry : IInteractor<ChangeGiftListExpiryRequest, ChangeGiftListExpiryResponse>;
