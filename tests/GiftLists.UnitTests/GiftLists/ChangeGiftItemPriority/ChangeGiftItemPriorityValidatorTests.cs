using GiftLists.Application.GiftLists.ChangeGiftItemPriority;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftItemPriority;

public sealed class ChangeGiftItemPriorityValidatorTests
{
    private static readonly ChangeGiftItemPriorityValidator Validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Validate_ShouldSucceed_WhenPriorityIs1Or5(int priority)
    {
        // Arrange
        var request = new ChangeGiftItemPriorityRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), priority);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Validate_ShouldReturnItemPriorityInvalid_WhenPriorityIs0Or6(int priority)
    {
        // Arrange
        var request = new ChangeGiftItemPriorityRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), priority);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_priority_invalid", result.Error.Code);
        Assert.Equal("Priority must be between 1 and 5.", result.Error.Message);
        Assert.Equal(BuildingBlocks.Results.ErrorKind.Validation, result.Error.Kind);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Validate_ShouldReturnInvalidId_WhenAnyIdIsEmpty(bool emptyListId, bool emptyRequesterId, bool emptyItemId)
    {
        // Arrange — priority is also out of range, so this proves ids are checked first.
        var request = new ChangeGiftItemPriorityRequest(
            emptyListId ? Guid.Empty : Guid.NewGuid(),
            emptyRequesterId ? Guid.Empty : Guid.NewGuid(),
            emptyItemId ? Guid.Empty : Guid.NewGuid(),
            0);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }
}
