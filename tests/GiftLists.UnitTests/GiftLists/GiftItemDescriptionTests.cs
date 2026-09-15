using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class GiftItemDescriptionTests
{
    [Fact]
    public void Constructor_ShouldTrimTheValue()
    {
        // Arrange
        const string padded = "  Burr, not blade  ";

        // Act
        var description = new GiftItemDescription(padded);

        // Assert
        Assert.Equal("Burr, not blade", description.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new GiftItemDescription("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueExceedsMaxLength()
    {
        // Arrange
        var tooLong = new string('a', GiftItemDescription.MaxLength + 1);

        // Act
        var exception = Record.Exception(() => new GiftItemDescription(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldAccept_AValueAtExactlyMaxLength()
    {
        // Arrange
        var exactlyMax = new string('a', GiftItemDescription.MaxLength);

        // Act
        var description = new GiftItemDescription(exactlyMax);

        // Assert
        Assert.Equal(exactlyMax, description.Value);
    }

    [Fact]
    public void IsValidLength_ShouldReturnTrue_WhenValueIsNullOrWhitespace()
    {
        // Arrange — Description is optional; a length bound cannot be a rule about absence.

        // Act
        var validNull = GiftItemDescription.IsValidLength(null);
        var validWhitespace = GiftItemDescription.IsValidLength("   ");

        // Assert
        Assert.True(validNull);
        Assert.True(validWhitespace);
    }

    [Fact]
    public void IsValidLength_ShouldReturnFalse_WhenValueExceedsMaxLength()
    {
        // Arrange
        var tooLong = new string('a', GiftItemDescription.MaxLength + 1);

        // Act
        var isValid = GiftItemDescription.IsValidLength(tooLong);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Burr, not blade")]
    public void IsValidLength_ShouldAgreeWithTheConstructor_ForANonBlankValue(string value)
    {
        // Mirrors ExpiryDate.IsInFuture's own agreement test — the predicate the Application
        // validator calls and the constructor it stands in front of must answer about the same
        // value, or a validator-approved request can throw where a
        // giftlist.item_description_invalid Result was expected. Restricted to non-blank input:
        // IsValidLength treats blank as "no description" (valid), while the constructor treats a
        // blank ARGUMENT as invalid input rather than "absence" — the interactor is what
        // reconciles the two, normalising blank to null before ever calling the constructor
        // (AddGiftItemInteractor's own comment).
        var predicateAccepts = GiftItemDescription.IsValidLength(value);
        var exception = Record.Exception(() => new GiftItemDescription(value));

        Assert.Equal(predicateAccepts, exception is null);
    }

    [Fact]
    public void IsValidLength_ShouldAgreeWithTheConstructor_ForATooLongValue()
    {
        // See the non-blank overload above for why blank is excluded from this agreement check.
        var tooLong = new string('a', GiftItemDescription.MaxLength + 1);

        var predicateAccepts = GiftItemDescription.IsValidLength(tooLong);
        var exception = Record.Exception(() => new GiftItemDescription(tooLong));

        Assert.Equal(predicateAccepts, exception is null);
    }

    [Fact]
    public void Rehydrate_ShouldNotThrow_ForAValueExceedingMaxLength()
    {
        // Arrange — GL-74's length bound is new; a description already longer than MaxLength may
        // already be persisted, and reloading it must not turn tightening the rule into an
        // outage for that list (GiftItemDescription's own doc comment).
        var legacyValue = new string('a', GiftItemDescription.MaxLength + 1);

        // Act
        var exception = Record.Exception(() => GiftItemDescription.Rehydrate(legacyValue));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Rehydrate_ShouldPreserveTheGivenValue()
    {
        // Arrange
        var legacyValue = new string('a', GiftItemDescription.MaxLength + 1);

        // Act
        var description = GiftItemDescription.Rehydrate(legacyValue);

        // Assert
        Assert.Equal(legacyValue, description.Value);
    }
}
