using GiftLists.Application.GiftLists.RenameGiftList;

namespace GiftLists.UnitTests.GiftLists.RenameGiftList;

public sealed class RenameGiftListValidatorTests
{
    private static readonly RenameGiftListValidator Validator = new();

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new RenameGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "New Name");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenListIdIsEmpty()
    {
        // Arrange
        var request = new RenameGiftListRequest(Guid.Empty, Guid.NewGuid(), "New Name");

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
        var request = new RenameGiftListRequest(Guid.NewGuid(), Guid.Empty, "New Name");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnNameInvalid_WhenNameIsWhitespace()
    {
        // Arrange
        var request = new RenameGiftListRequest(Guid.NewGuid(), Guid.NewGuid(), "   ");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.name_invalid", result.Error.Code);
    }
}
