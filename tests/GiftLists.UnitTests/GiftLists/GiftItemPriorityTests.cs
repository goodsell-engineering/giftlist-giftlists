using GiftLists.Domain.GiftLists;

namespace GiftLists.UnitTests.GiftLists;

public sealed class GiftItemPriorityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_ShouldAccept_WhenTheValueIsAtABoundary(int value)
    {
        // Arrange — none

        // Act
        var priority = new GiftItemPriority(value);

        // Assert
        Assert.Equal(value, priority.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Constructor_ShouldThrow_WhenOutside1To5(int value)
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new GiftItemPriority(value));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Default_ShouldBe3()
    {
        // Arrange — none

        // Act
        var value = GiftItemPriority.Default.Value;

        // Assert
        Assert.Equal(3, value);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void IsValid_ShouldMatchTheConstructorRule(int value, bool expected)
    {
        // Arrange — none

        // Act
        var valid = GiftItemPriority.IsValid(value);

        // Assert
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Equals_ShouldCompareByValue()
    {
        // Arrange
        var a = new GiftItemPriority(4);
        var b = new GiftItemPriority(4);

        // Act + Assert
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new GiftItemPriority(2));
    }
}
