using System.Reflection;
using GiftLists.Contracts.GiftLists.Events;

namespace GiftLists.UnitTests.GiftLists.Messaging;

/// <summary>Pins <see cref="GiftItemAddedV1"/> to its original six fields plus a nullable Priority appended last (TC-GL-146-18).</summary>
public sealed class GiftItemAddedV1Tests
{
    [Fact]
    public void GiftItemAddedV1_ShouldCarryExactlyItsSixFieldsPlusANullablePriorityLast()
    {
        // Arrange
        var expected = new[] { "ListId", "ItemId", "Name", "Description", "Url", "AddedAt", "Priority" };

        // Act — GetProperties order is not guaranteed, so "last" is read off the primary
        // constructor's parameter list, which is declaration order.
        var parameters = typeof(GiftItemAddedV1).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == expected.Length)
            .GetParameters();
        var properties = typeof(GiftItemAddedV1).GetProperties(BindingFlags.Public | BindingFlags.Instance)
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
