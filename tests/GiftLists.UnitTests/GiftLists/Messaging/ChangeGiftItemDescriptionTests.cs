using System.Reflection;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>
/// Pins <see cref="global::GiftLists.Contracts.GiftLists.ChangeGiftItemDescription"/>'s exact field set
/// (TC-T2-28) — a reflection allow-list, modelled on
/// <see cref="GiftItemDescriptionChangedV1Tests"/>, so a field silently added or renamed on the
/// command fails here rather than being noticed only once it reaches the wire carrying something
/// privacy-sensitive it should not.
/// </summary>
public sealed class ChangeGiftItemDescriptionTests
{
    [Fact]
    public void ChangeGiftItemDescription_ShouldCarryExactlyListIdRequesterIdItemIdAndDescription()
    {
        // Arrange
        var expected = new[] { "ListId", "RequesterId", "ItemId", "Description" };

        // Act
        var actual = typeof(global::GiftLists.Contracts.GiftLists.ChangeGiftItemDescription)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual);
    }
}
