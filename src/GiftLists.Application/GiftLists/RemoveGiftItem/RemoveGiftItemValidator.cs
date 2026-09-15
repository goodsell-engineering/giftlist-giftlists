using BuildingBlocks.Results;
using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.RemoveGiftItem;

internal sealed class RemoveGiftItemValidator : IValidator<RemoveGiftItemRequest>
{
    public Result Validate(RemoveGiftItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        return Result.Success();
    }
}
