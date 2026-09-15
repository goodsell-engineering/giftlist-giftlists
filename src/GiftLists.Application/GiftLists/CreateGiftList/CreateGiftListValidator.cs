using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.CreateGiftList;

/// <summary>
/// Injects <see cref="IClock"/> to check "expiry must be in the future" before construction ever
/// touches <c>ExpiryDate</c> — nothing about <see cref="IValidator{TRequest}"/> restricts a
/// validator to being a pure function of its request alone.
/// </summary>
internal sealed class CreateGiftListValidator(IClock clock) : IValidator<CreateGiftListRequest>
{
    public Result Validate(CreateGiftListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.OwnerId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!GiftListName.IsValidLength(request.Name))
        {
            return GiftListErrors.NameInvalid;
        }

        if (!ExpiryDate.IsInFuture(request.ExpiresAt, clock.UtcNow))
        {
            return GiftListErrors.ExpiryInvalid;
        }

        return Result.Success();
    }
}
