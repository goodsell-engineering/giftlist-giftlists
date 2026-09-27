using System.Reflection;
using GiftLists.Contracts.GiftLists.Events;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>
/// Pins <see cref="GiftItemDescriptionChangedV1"/>'s exact field set (TC-T2-20) — a reflection
/// allow-list, so a field silently added or renamed on the wire type fails here rather than being
/// noticed only by whichever consumer happens to break.
/// </summary>
public sealed class GiftItemDescriptionChangedV1Tests
{
    [Fact]
    public void GiftItemDescriptionChangedV1_ShouldCarryExactlyListIdItemIdDescriptionAndChangedAt()
    {
        // Arrange
        var expected = new[] { "ListId", "ItemId", "Description", "ChangedAt" };

        // Act
        var actual = typeof(GiftItemDescriptionChangedV1)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual);
    }
}
