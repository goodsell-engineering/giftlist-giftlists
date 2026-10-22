using GiftLists.Domain.GiftLists;
using GiftLists.Infrastructure.GiftLists.Persistence;

namespace GiftLists.UnitTests.GiftLists.Persistence;

public sealed class GiftListToUpdateMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static GiftList ListWithItems(params GiftItemId[] itemIds)
    {
        var list = GiftList.Create(
            GiftListId.New(), new OwnerId(Guid.NewGuid()), new GiftListName("Wishlist"),
            new ExpiryDate(Now.AddDays(7), Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        foreach (var itemId in itemIds)
        {
            list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, GiftItemPriority.Default, Now);
        }

        list.ClearDomainEvents();
        return list;
    }

    [Fact]
    public void ToUpdate_ShouldThrow_WhenOneSaveChangesADescriptionAndAPriorityOnDifferentItems()
    {
        // Arrange — both writes share the one array filter "i", so two items would write to the wrong element.
        var first = GiftItemId.New();
        var second = GiftItemId.New();
        var list = ListWithItems(first, second);
        list.ChangeItemDescription(first, new GiftItemDescription("Size M"), Now);
        list.ChangeItemPriority(second, new GiftItemPriority(5), Now);

        // Act
        var exception = Record.Exception(() => GiftListToUpdateMapper.ToUpdate(list));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void ToUpdate_ShouldSucceed_WhenOneSaveChangesADescriptionAndAPriorityOnTheSameItem()
    {
        // Arrange
        var itemId = GiftItemId.New();
        var list = ListWithItems(itemId);
        list.ChangeItemDescription(itemId, new GiftItemDescription("Size M"), Now);
        list.ChangeItemPriority(itemId, new GiftItemPriority(5), Now);

        // Act
        var update = GiftListToUpdateMapper.ToUpdate(list);

        // Assert
        Assert.NotNull(update);
        Assert.Single(update.ArrayFilters!);
    }
}
