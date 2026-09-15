using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.RenameGiftList;

/// <summary>The named input port for "rename a gift list" — see <c>ICreateGiftList</c> for why nothing resolves this specific type from the container.</summary>
public interface IRenameGiftList : IInteractor<RenameGiftListRequest, RenameGiftListResponse>;
