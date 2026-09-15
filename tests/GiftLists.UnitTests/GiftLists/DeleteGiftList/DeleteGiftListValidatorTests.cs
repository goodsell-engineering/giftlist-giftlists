using GiftLists.Application.GiftLists.DeleteGiftList;

namespace GiftLists.UnitTests.GiftLists.DeleteGiftList;

public sealed class DeleteGiftListValidatorTests
{
    private static readonly DeleteGiftListValidator Validator = new();

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new DeleteGiftListRequest(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenListIdIsEmpty()
    {
        // Arrange
        var request = new DeleteGiftListRequest(Guid.Empty, Guid.NewGuid());

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
        var request = new DeleteGiftListRequest(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }
}
