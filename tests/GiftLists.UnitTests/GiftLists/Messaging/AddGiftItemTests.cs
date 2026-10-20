using System.Reflection;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>Pins <see cref="global::GiftLists.Contracts.GiftLists.AddGiftItem"/> to its original six fields plus a nullable Priority appended last (TC-GL-146-19).</summary>
public sealed class AddGiftItemTests
{
    [Fact]
    public void AddGiftItem_ShouldCarryExactlyItsSixFieldsPlusANullablePriority()
    {
        // Arrange
        var expected = new[] { "ListId", "RequesterId", "ItemId", "Name", "Description", "Url", "Priority" };

        // Act
        var type = typeof(global::GiftLists.Contracts.GiftLists.AddGiftItem);
        var parameters = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == expected.Length)
            .GetParameters();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Equal(expected, parameters.Select(p => p.Name).ToArray());
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), properties);
        Assert.Equal(typeof(int?), parameters[^1].ParameterType);
        Assert.True(parameters[^1].HasDefaultValue);
    }
}
