using GiftLists.Application.GiftLists.RemoveGiftItem;

namespace GiftLists.UnitTests.GiftLists.RemoveGiftItem;

public sealed class RemoveGiftItemValidatorTests
{
    private static readonly RemoveGiftItemValidator Validator = new();

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new RemoveGiftItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenItemIdIsEmpty()
    {
        // Arrange
        var request = new RemoveGiftItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }
}
