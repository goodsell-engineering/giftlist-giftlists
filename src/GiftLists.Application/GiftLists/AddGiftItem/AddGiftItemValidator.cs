using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.AddGiftItem;

internal sealed class AddGiftItemValidator : IValidator<AddGiftItemRequest>
{
    public Result Validate(AddGiftItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty || request.ItemId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!GiftItemName.IsValidLength(request.Name))
        {
            return GiftListErrors.ItemNameInvalid;
        }

        // GL-74: Url and Description were validated nowhere — the CONVENTIONS.md "Domain modelling" breach this
        // issue exists to close. See GiftItemUrl.IsValid/GiftItemDescription.IsValidLength for
        // what each rule is.
        if (!GiftItemUrl.IsValid(request.Url))
        {
            return GiftListErrors.ItemUrlInvalid;
        }

        if (!GiftItemDescription.IsValidLength(request.Description))
        {
            return GiftListErrors.ItemDescriptionInvalid;
        }

        return Result.Success();
    }
}
