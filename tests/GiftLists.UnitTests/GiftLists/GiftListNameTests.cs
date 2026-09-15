using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class GiftListNameTests
{
    [Fact]
    public void Constructor_ShouldTrimTheValue()
    {
        // Arrange
        const string padded = "  Birthday Wishlist  ";

        // Act
        var name = new GiftListName(padded);

        // Assert
        Assert.Equal("Birthday Wishlist", name.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new GiftListName("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueExceedsMaxLength()
    {
        // Arrange
        var tooLong = new string('a', 101);

        // Act
        var exception = Record.Exception(() => new GiftListName(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldAccept_AValueAtExactlyMaxLength()
    {
        // Arrange
        var exactlyMax = new string('a', 100);

        // Act
        var name = new GiftListName(exactlyMax);

        // Assert
        Assert.Equal(exactlyMax, name.Value);
    }

    [Fact]
    public void IsValidLength_ShouldReturnFalse_WhenValueIsNullOrTooLong()
    {
        // Arrange
        var tooLong = new string('a', 101);

        // Act
        var validNull = GiftListName.IsValidLength(null);
        var validTooLong = GiftListName.IsValidLength(tooLong);

        // Assert
        Assert.False(validNull);
        Assert.False(validTooLong);
    }
}
