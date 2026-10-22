using System.Reflection;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>Pins <see cref="global::GiftLists.Contracts.GiftLists.ChangeGiftItemPriority"/>'s exact field set (TC-GL-146-17), same technique as <see cref="ChangeGiftItemDescriptionTests"/>.</summary>
public sealed class ChangeGiftItemPriorityTests
{
    [Fact]
    public void ChangeGiftItemPriority_ShouldCarryExactlyListIdRequesterIdItemIdAndPriority()
    {
        // Arrange
        var expected = new[] { "ListId", "RequesterId", "ItemId", "Priority" };

        // Act
        var actual = typeof(global::GiftLists.Contracts.GiftLists.ChangeGiftItemPriority)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual);
    }
}
