using System.Reflection;
using GiftLists.Contracts.GiftLists.Events;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>Pins <see cref="GiftItemPriorityChangedV1"/>'s exact field set (TC-GL-146-16), same technique as <see cref="GiftItemDescriptionChangedV1Tests"/>.</summary>
public sealed class GiftItemPriorityChangedV1Tests
{
    [Fact]
    public void GiftItemPriorityChangedV1_ShouldCarryExactlyListIdItemIdPriorityAndChangedAt()
    {
        // Arrange
        var expected = new[] { "ListId", "ItemId", "Priority", "ChangedAt" };

        // Act
        var properties = typeof(GiftItemPriorityChangedV1).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var actual = properties.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // Assert
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual);
        Assert.Equal(typeof(int), properties.Single(p => p.Name == "Priority").PropertyType);
    }
}
