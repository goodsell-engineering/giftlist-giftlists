using BuildingBlocks.Results;
using GiftLists.Application.Common;
using GiftLists.Domain.GiftLists;

namespace GiftLists.Application.GiftLists.ChangeGiftListExpiry;

/// <summary>
/// Injects <see cref="IClock"/> for the same reason <c>CreateGiftListValidator</c> does: "expiry
/// must be in the future" is checked here, as a <c>Result</c>, before construction ever touches
/// <c>ExpiryDate</c>'s throwing constructor (CONVENTIONS.md "Errors").
/// </summary>
internal sealed class ChangeGiftListExpiryValidator(IClock clock) : IValidator<ChangeGiftListExpiryRequest>
{
    public Result Validate(ChangeGiftListExpiryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ListId == Guid.Empty || request.RequesterId == Guid.Empty)
        {
            return GiftListErrors.InvalidId;
        }

        if (!ExpiryDate.IsInFuture(request.ExpiresAt, clock.UtcNow))
        {
            return GiftListErrors.ExpiryInvalid;
        }

        return Result.Success();
    }
}
