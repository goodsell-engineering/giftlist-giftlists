using GiftLists.Application.GiftLists.AddGiftItem;
using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists.AddGiftItem;

public sealed class AddGiftItemValidatorTests
{
    private static readonly AddGiftItemValidator Validator = new();

    [Fact]
    public void Validate_ShouldSucceed_ForAWellFormedRequest()
    {
        // Arrange
        var request = new AddGiftItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Coffee grinder", null, null);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_ShouldReturnInvalidId_WhenItemIdIsEmpty()
    {
        // Arrange
        var request = new AddGiftItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Coffee grinder", null, null);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.invalid_id", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnItemNameInvalid_WhenNameIsWhitespace()
    {
        // Arrange
        var request = new AddGiftItemRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "   ", null, null);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_name_invalid", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUrlIsAnAbsoluteHttpsUrl()
    {
        // Arrange
        var request = new AddGiftItemRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Coffee grinder", null, "https://example.com/grinder");

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("//evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url-at-all")]
    public void Validate_ShouldReturnItemUrlInvalid_WhenUrlIsNotAnAbsoluteHttpUrl(string url)
    {
        // Arrange — GL-74: Url was validated nowhere; "//evil.example/" is the ticket's own
        // headline vector, an unvalidated protocol-relative URL silently resolving to a
        // third-party origin.
        var request = new AddGiftItemRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Coffee grinder", null, url);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_url_invalid", result.Error.Code);
    }

    [Fact]
    public void Validate_ShouldReturnItemDescriptionInvalid_WhenDescriptionExceedsMaxLength()
    {
        // Arrange — GL-74's length-bound decision: Description was an unbounded-write vector
        // into both this aggregate's own Mongo document and the Gateway's read projection.
        var tooLong = new string('a', GiftItemDescription.MaxLength + 1);
        var request = new AddGiftItemRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Coffee grinder", tooLong, null);

        // Act
        var result = Validator.Validate(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_description_invalid", result.Error.Code);
    }
}
