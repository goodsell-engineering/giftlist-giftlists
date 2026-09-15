using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class ShareTokenTests
{
    [Fact]
    public void Constructor_ShouldAccept_A21CharacterBase62Value()
    {
        // Arrange
        var value = new string('a', ShareToken.Length);

        // Act
        var token = new ShareToken(value);

        // Assert
        Assert.Equal(value, token.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsTooShort()
    {
        // Arrange
        var tooShort = new string('a', ShareToken.Length - 1);

        // Act
        var exception = Record.Exception(() => new ShareToken(tooShort));

        // Assert
        var argumentException = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("value", argumentException.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsTooLong()
    {
        // Arrange
        var tooLong = new string('a', ShareToken.Length + 1);

        // Act
        var exception = Record.Exception(() => new ShareToken(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueContainsANonBase62Character()
    {
        // Arrange
        var withHyphen = new string('a', ShareToken.Length - 1) + "-";

        // Act
        var exception = Record.Exception(() => new ShareToken(withHyphen));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new ShareToken("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForTwoTokensWithTheSameValue()
    {
        // Arrange
        var value = new string('b', ShareToken.Length);
        var first = new ShareToken(value);
        var second = new ShareToken(value);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
    }
}
