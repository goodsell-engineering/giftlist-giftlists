using GiftLists.Application.GiftLists.ChangeGiftItemDescription;
using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftItemDescription;

public sealed class ChangeGiftItemDescriptionValidatorTests
{
    private static readonly ChangeGiftItemDescriptionValidator Validator = new();

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new ChangeGiftItemDescriptionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Size M");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Validate_ShouldReturnInvalidId_WhenAnyIdIsEmpty(bool emptyListId, bool emptyRequesterId, bool emptyItemId)
    {
        // Arrange
        var request = new ChangeGiftItemDescriptionRequest(
            emptyListId ? Guid.Empty : Guid.NewGuid(),
            emptyRequesterId ? Guid.Empty : Guid.NewGuid(),
            emptyItemId ? Guid.Empty : Guid.NewGuid(),
            "Size M");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnItemDescriptionInvalid_WhenLongerThan2000CharactersAfterTrimming()
    {
        // Arrange — padding either side of the limit with whitespace, so this fails only if the
        // validator trims before measuring, matching GiftItemDescription's own constructor rule.
        var tooLong = "  " + new string('a', GiftItemDescription.MaxLength + 1) + "  ";
        var request = new ChangeGiftItemDescriptionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), tooLong);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_description_invalid", result.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldSucceed_WhenBlankOrNull(string? description)
    {
        // Arrange — blank/null means "clear the description", which is always valid.
        var request = new ChangeGiftItemDescriptionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), description);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenExactly2000Characters()
    {
        // Arrange
        var exactly2000 = new string('a', GiftItemDescription.MaxLength);
        var request = new ChangeGiftItemDescriptionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), exactly2000);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
