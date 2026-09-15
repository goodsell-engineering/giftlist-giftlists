using GiftLists.Application.GiftLists.CreateGiftList;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.CreateGiftList;

public sealed class CreateGiftListValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CreateGiftListValidator CreateValidator() => new(new FakeClock(Now));

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var validator = CreateValidator();
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenListIdIsEmpty()
    {
        // Arrange
        var validator = CreateValidator();
        var request = new CreateGiftListRequest(Guid.Empty, Guid.NewGuid(), "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenOwnerIdIsEmpty()
    {
        // Arrange
        var validator = CreateValidator();
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.Empty, "Birthday Wishlist", Now.AddDays(7));

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnNameInvalid_WhenNameIsWhitespace()
    {
        // Arrange
        var validator = CreateValidator();
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "   ", Now.AddDays(7));

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.name_invalid", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnExpiryInvalid_WhenExpiresAtIsNotInTheFuture()
    {
        // Arrange
        var validator = CreateValidator();
        var request = new CreateGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "Birthday Wishlist", Now);

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.expiry_invalid", result.Error.Code);
    }
}
