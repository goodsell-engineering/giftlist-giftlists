using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftItemPriority;

internal sealed class ChangeGiftItemPriorityValidator : IValidator<ChangeGiftItemPriorityRequest>
{
    public Result Validate(ChangeGiftItemPriorityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!GiftItemPriority.IsValid(request.Priority))
        {
            return GiftListErrors.ItemPriorityInvalid;
        }

        return Result.Success();
    }
}
