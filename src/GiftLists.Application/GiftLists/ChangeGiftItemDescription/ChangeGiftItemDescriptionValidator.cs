using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftItemDescription;

internal sealed class ChangeGiftItemDescriptionValidator : IValidator<ChangeGiftItemDescriptionRequest>
{
    public Result Validate(ChangeGiftItemDescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!GiftItemDescription.IsValidLength(request.Description))
        {
            return GiftListErrors.ItemDescriptionInvalid;
        }

        return Result.Success();
    }
}
