using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.RenameGiftList;

internal sealed class RenameGiftListValidator : IValidator<RenameGiftListRequest>
{
    public Result Validate(RenameGiftListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!GiftListName.IsValidLength(request.Name))
        {
            return GiftListErrors.NameInvalid;
        }

        return Result.Success();
    }
}
