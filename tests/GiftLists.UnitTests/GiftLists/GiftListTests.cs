using GiftLists.Domain.GiftLists;
using GiftLists.Domain.GiftLists.Events;

namespace GiftLists.UnitTests.GiftLists;

/// <summary>
/// <see cref="GiftList.Create"/> vs. <see cref="GiftList.Rehydrate"/>, and the two invariants
/// enforced on the aggregate itself (CONVENTIONS.md "Domain modelling") rather than only pre-checked by an
/// interactor: <see cref="GiftList.AddItem"/> refusing an expired list
/// (CONVENTIONS.md "Domain modelling") and <see cref="GiftList.RemoveItem"/> refusing an unknown item id.
/// The integration suite only ever drives these through an interactor that pre-checks first, so
/// the throw path itself — the backstop for a missed check — is a gap only this suite reaches
/// (CONVENTIONS.md "Testing").
/// </summary>
public sealed class GiftListTests
{
    private static readonly GiftListId ListId = GiftListId.New();
    private static readonly OwnerId OwnerId = new(Guid.NewGuid());
    private static readonly GiftListName Name = new("Birthday Wishlist");
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ExpiryDate Expiry = new(Now.AddDays(7), Now);
    private static readonly ShareToken ShareToken = new(new string('a', ShareToken.Length));

    private static GiftList CreateList() => GiftList.Create(ListId, OwnerId, Name, Expiry, ShareToken, Now);

    [Fact]
    public void Create_ShouldRaiseAGiftListCreatedEvent_WithTheGivenFields()
    {
        // Arrange — none

        // Act
        var list = CreateList();

        // Assert
        var domainEvent = Assert.Single(list.DomainEvents);
        var created = Assert.IsType<GiftListCreated>(domainEvent);
        Assert.Equal(ListId, created.ListId);
        Assert.Equal(OwnerId, created.OwnerId);
        Assert.Equal(Name, created.Name);
        Assert.Equal(Expiry, created.Expiry);
        Assert.Equal(ShareToken, created.ShareToken);
        Assert.Equal(Now, created.CreatedAt);
    }

    [Fact]
    public void Rehydrate_ShouldRaiseNoDomainEvents()
    {
        // Arrange — none

        // Act
        var list = GiftList.Rehydrate(ListId, OwnerId, Name, Expiry, ShareToken, Now, version: 0, []);

        // Assert
        Assert.Empty(list.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheCollection_AfterCreate()
    {
        // Arrange
        var list = CreateList();

        // Act
        list.ClearDomainEvents();

        // Assert
        Assert.Empty(list.DomainEvents);
    }

    [Fact]
    public void ChangeExpiry_ShouldUpdateExpiryAndRaiseAGiftListExpiryChangedEvent()
    {
        // Arrange
        var list = CreateList();
        list.ClearDomainEvents();
        var newExpiry = new ExpiryDate(Now.AddDays(30), Now);
        var changedAt = Now.AddDays(1);

        // Act
        list.ChangeExpiry(newExpiry, changedAt);

        // Assert
        Assert.Equal(newExpiry, list.Expiry);
        Assert.Equal(1, list.Version);
        var domainEvent = Assert.Single(list.DomainEvents);
        var changed = Assert.IsType<GiftListExpiryChanged>(domainEvent);
        Assert.Equal(ListId, changed.ListId);
        Assert.Equal(newExpiry, changed.Expiry);
        Assert.Equal(changedAt, changed.ChangedAt);
    }

    [Fact]
    public void Rename_ShouldUpdateNameAndRaiseAGiftListRenamedEvent()
    {
        // Arrange
        var list = CreateList();
        list.ClearDomainEvents();
        var newName = new GiftListName("Housewarming Wishlist");
        var renamedAt = Now.AddDays(1);

        // Act
        list.Rename(newName, renamedAt);

        // Assert
        Assert.Equal(newName, list.Name);
        var domainEvent = Assert.Single(list.DomainEvents);
        var renamed = Assert.IsType<GiftListRenamed>(domainEvent);
        Assert.Equal(ListId, renamed.ListId);
        Assert.Equal(newName, renamed.Name);
        Assert.Equal(renamedAt, renamed.RenamedAt);
    }

    [Fact]
    public void Delete_ShouldRaiseAGiftListDeletedEvent()
    {
        // Arrange
        var list = CreateList();
        list.ClearDomainEvents();
        var deletedAt = Now.AddDays(1);

        // Act
        list.Delete(deletedAt);

        // Assert
        var domainEvent = Assert.Single(list.DomainEvents);
        var deleted = Assert.IsType<GiftListDeleted>(domainEvent);
        Assert.Equal(ListId, deleted.ListId);
        Assert.Equal(deletedAt, deleted.DeletedAt);
    }

    [Fact]
    public void AddItem_ShouldAddTheItemAndRaiseAGiftItemAddedEvent_WhenListIsNotExpired()
    {
        // Arrange
        var list = CreateList();
        list.ClearDomainEvents();
        var itemId = GiftItemId.New();
        var itemName = new GiftItemName("Coffee grinder");

        var description = new GiftItemDescription("Burr, not blade");
        var url = new GiftItemUrl("https://example.com/grinder");

        // Act
        list.AddItem(itemId, itemName, description, url, Now);

        // Assert
        var item = Assert.Single(list.Items);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(itemName, item.Name);
        Assert.Equal(description, item.Description);
        Assert.Equal(url, item.Url);
        var domainEvent = Assert.Single(list.DomainEvents);
        var added = Assert.IsType<GiftItemAdded>(domainEvent);
        Assert.Equal(ListId, added.ListId);
        Assert.Equal(itemId, added.ItemId);
        Assert.Equal(itemName, added.Name);
    }

    [Fact]
    public void AddItem_ShouldNotThrow_WhenAddedAtIsOneTickBeforeExpiry()
    {
        // Arrange — the boundary just inside "not yet expired": HasExpired is Value <= now, so
        // one tick before Expiry.Value must still succeed.
        var list = CreateList();
        var justBeforeExpiry = Expiry.Value.AddTicks(-1);

        // Act
        var exception = Record.Exception(() =>
            list.AddItem(GiftItemId.New(), new GiftItemName("Just in time"), null, null, justBeforeExpiry));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddItem_ShouldThrowInvalidOperationException_WhenAddedAtEqualsExpiry()
    {
        // Arrange — the boundary itself: HasExpired treats Value <= now as expired, so the exact
        // expiry instant must already be rejected, not just a "clearly past" date.
        var list = CreateList();

        // Act
        var exception = Record.Exception(() =>
            list.AddItem(GiftItemId.New(), new GiftItemName("Too late"), null, null, Expiry.Value));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void AddItem_ShouldThrowInvalidOperationException_WhenAddedAtIsWellAfterExpiry()
    {
        // Arrange
        var list = CreateList();
        var wellAfterExpiry = Expiry.Value.AddDays(30);

        // Act
        var exception = Record.Exception(() =>
            list.AddItem(GiftItemId.New(), new GiftItemName("Way too late"), null, null, wellAfterExpiry));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void RemoveItem_ShouldRemoveTheItemAndRaiseAGiftItemRemovedEvent_WhenItemExists()
    {
        // Arrange
        var list = CreateList();
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, Now);
        list.ClearDomainEvents();
        var removedAt = Now.AddDays(1);

        // Act
        list.RemoveItem(itemId, removedAt);

        // Assert
        Assert.Empty(list.Items);
        var domainEvent = Assert.Single(list.DomainEvents);
        var removed = Assert.IsType<GiftItemRemoved>(domainEvent);
        Assert.Equal(ListId, removed.ListId);
        Assert.Equal(itemId, removed.ItemId);
        Assert.Equal(removedAt, removed.RemovedAt);
    }

    [Fact]
    public void RemoveItem_ShouldThrowInvalidOperationException_WhenItemDoesNotExist()
    {
        // Arrange
        var list = CreateList();
        var unknownItemId = GiftItemId.New();

        // Act
        var exception = Record.Exception(() => list.RemoveItem(unknownItemId, Now));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Create_ShouldRaiseGiftListCreated_CarryingTheInstantTheAggregateKept()
    {
        // Arrange — a createdAt that is NOT millisecond-aligned. Every other test here uses `Now`,
        // which is aligned, so normalisation is a no-op and none of them can see this: the event was
        // built from the raw constructor argument rather than the aggregate's normalised CreatedAt,
        // so GiftListCreatedV1 reached Gateway and Reservation carrying an instant that disagreed
        // with what giftlist.giftLists held — the drift the normalisation exists to remove,
        // reintroduced one hop further out (Batch 11 review).
        var unaligned = Now.AddTicks(7_777);

        // Act
        var list = GiftList.Create(ListId, OwnerId, Name, Expiry, ShareToken, unaligned);

        // Assert
        var created = Assert.IsType<GiftListCreated>(Assert.Single(list.DomainEvents));
        Assert.Equal(list.CreatedAt, created.CreatedAt);
        Assert.Equal(0, created.CreatedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(created.CreatedAt <= unaligned);
    }

    [Fact]
    public void Rename_ShouldRaiseGiftListRenamed_WithAMillisecondAlignedTimestamp()
    {
        // Arrange — deliberately unaligned, same reasoning as Create's own version of this test
        // (GL-66): every existing test here uses `Now`, which is already millisecond-aligned, so
        // normalisation is a no-op none of them could catch.
        var list = CreateList();
        list.ClearDomainEvents();
        var unaligned = Now.AddTicks(7_777);

        // Act
        list.Rename(new GiftListName("Housewarming Wishlist"), unaligned);

        // Assert
        var renamed = Assert.IsType<GiftListRenamed>(Assert.Single(list.DomainEvents));
        Assert.Equal(0, renamed.RenamedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(renamed.RenamedAt <= unaligned);
    }

    [Fact]
    public void Delete_ShouldRaiseGiftListDeleted_WithAMillisecondAlignedTimestamp()
    {
        // Arrange — deliberately unaligned, same reasoning as Rename's version above (GL-66).
        var list = CreateList();
        list.ClearDomainEvents();
        var unaligned = Now.AddTicks(7_777);

        // Act
        list.Delete(unaligned);

        // Assert
        var deleted = Assert.IsType<GiftListDeleted>(Assert.Single(list.DomainEvents));
        Assert.Equal(0, deleted.DeletedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(deleted.DeletedAt <= unaligned);
    }

    [Fact]
    public void AddItem_ShouldRaiseGiftItemAdded_WithAMillisecondAlignedTimestamp()
    {
        // Arrange — deliberately unaligned, same reasoning as Rename's version above (GL-66).
        var list = CreateList();
        list.ClearDomainEvents();
        var unaligned = Now.AddTicks(7_777);

        // Act
        list.AddItem(GiftItemId.New(), new GiftItemName("Coffee grinder"), null, null, unaligned);

        // Assert
        var added = Assert.IsType<GiftItemAdded>(Assert.Single(list.DomainEvents));
        Assert.Equal(0, added.AddedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(added.AddedAt <= unaligned);
    }

    [Fact]
    public void RemoveItem_ShouldRaiseGiftItemRemoved_WithAMillisecondAlignedTimestamp()
    {
        // Arrange — deliberately unaligned, same reasoning as Rename's version above (GL-66).
        var list = CreateList();
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, Now);
        list.ClearDomainEvents();
        var unaligned = Now.AddTicks(7_777);

        // Act
        list.RemoveItem(itemId, unaligned);

        // Assert
        var removed = Assert.IsType<GiftItemRemoved>(Assert.Single(list.DomainEvents));
        Assert.Equal(0, removed.RemovedAt.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(removed.RemovedAt <= unaligned);
    }
}
