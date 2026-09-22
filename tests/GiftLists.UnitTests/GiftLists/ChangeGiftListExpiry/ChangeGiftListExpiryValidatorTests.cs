using GiftLists.Application.GiftLists.ChangeGiftListExpiry;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftListExpiry;

public sealed class ChangeGiftListExpiryValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ChangeGiftListExpiryValidator Validator = new(new FakeClock(Now));

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new ChangeGiftListExpiryRequest(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(30));

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenListIdIsEmpty()
    {
        // Arrange
        var request = new ChangeGiftListExpiryRequest(Guid.Empty, Guid.NewGuid(), Now.AddDays(30));

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenRequesterIdIsEmpty()
    {
        // Arrange
        var request = new ChangeGiftListExpiryRequest(Guid.NewGuid(), Guid.Empty, Now.AddDays(30));

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnExpiryInvalid_WhenTheNewExpiryIsNotInTheFuture()
    {
        // Arrange
        var request = new ChangeGiftListExpiryRequest(Guid.NewGuid(), Guid.NewGuid(), Now);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.expiry_invalid", result.Error.Code);
    }
}
