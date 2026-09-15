using BuildingBlocks.Results;
using GiftLists.Application.Common;

namespace GiftLists.Application.GiftLists.DeleteGiftList;

internal sealed class DeleteGiftListValidator : IValidator<DeleteGiftListRequest>
{
    public Result Validate(DeleteGiftListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        return Result.Success();
    }
}
