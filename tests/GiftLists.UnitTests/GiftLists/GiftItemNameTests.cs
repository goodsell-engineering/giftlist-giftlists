using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class GiftItemNameTests
{
    [Fact]
    public void Constructor_ShouldTrimTheValue()
    {
        // Arrange
        const string padded = "  Coffee grinder  ";

        // Act
        var name = new GiftItemName(padded);

        // Assert
        Assert.Equal("Coffee grinder", name.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new GiftItemName("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueExceedsMaxLength()
    {
        // Arrange
        var tooLong = new string('a', 201);

        // Act
        var exception = Record.Exception(() => new GiftItemName(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldAccept_AValueAtExactlyMaxLength()
    {
        // Arrange
        var exactlyMax = new string('a', 200);

        // Act
        var name = new GiftItemName(exactlyMax);

        // Assert
        Assert.Equal(exactlyMax, name.Value);
    }

    [Fact]
    public void IsValidLength_ShouldReturnFalse_WhenValueIsNullOrTooLong()
    {
        // Arrange
        var tooLong = new string('a', 201);

        // Act
        var validNull = GiftItemName.IsValidLength(null);
        var validTooLong = GiftItemName.IsValidLength(tooLong);

        // Assert
        Assert.False(validNull);
        Assert.False(validTooLong);
    }
}
