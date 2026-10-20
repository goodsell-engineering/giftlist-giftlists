using GiftLists.Application.GiftLists.ChangeGiftItemPriority;
using GiftLists.Domain.GiftLists;
using GiftLists.UnitTests.Support;

namespace GiftLists.UnitTests.GiftLists.ChangeGiftItemPriority;

public sealed class ChangeGiftItemPriorityInteractorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly OwnerId Owner = new(Guid.NewGuid());

    private static (GiftList List, GiftItemId ItemId) SeededListWithItem(
        FakeGiftListRepository repository, OwnerId ownerId, int priority = 3)
    {
        var list = GiftList.Create(
            GiftListId.New(), ownerId, new GiftListName("Birthday Wishlist"),
            new ExpiryDate(Now.AddDays(7), Now), new ShareToken(new string('a', ShareToken.Length)), Now);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, new GiftItemPriority(priority), Now);
        list.ClearDomainEvents();
        repository.Seed(list);
        return (list, itemId);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNoListExistsWithTheGivenId()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var interactor = new ChangeGiftItemPriorityInteractor(repository, new FakeDomainEventPublisher(), new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(Guid.NewGuid(), Owner.Value, Guid.NewGuid(), 5);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbiddenAndPublishNothing_WhenRequesterIsNotTheOwner()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, itemId) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemPriorityInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(list.Id.Value, Guid.NewGuid(), itemId.Value, 5);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.forbidden", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
    }

    [Fact]
    public async Task Handle_ShouldReturnItemNotFound_WhenTheItemIsNotOnTheList()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, _) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemPriorityInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(list.Id.Value, Owner.Value, Guid.NewGuid(), 5);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.item_not_found", result.Error.Code);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldReturnExpiredAndPublishNothing_WhenTheListHasExpired()
    {
        // Arrange — Rehydrate, not Create: ExpiryDate's validating constructor refuses a past date.
        var repository = new FakeGiftListRepository();
        var list = GiftList.Rehydrate(
            GiftListId.New(), Owner, new GiftListName("Wishlist"),
            ExpiryDate.Rehydrate(Now.AddDays(-1)), new ShareToken(new string('a', ShareToken.Length)),
            Now.AddDays(-30), version: 0, []);
        var itemId = GiftItemId.New();
        list.AddItem(itemId, new GiftItemName("Coffee grinder"), null, null, GiftItemPriority.Default, Now.AddDays(-30));
        repository.Seed(list);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemPriorityInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(list.Id.Value, Owner.Value, itemId.Value, 5);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("giftlist.expired", result.Error.Code);
        Assert.Empty(publisher.PublishedBatches);
    }

    [Fact]
    public async Task Handle_ShouldSaveNothingAndPublishNothing_WhenThePriorityEqualsTheCurrentOne()
    {
        // Arrange
        var repository = new FakeGiftListRepository();
        var (list, itemId) = SeededListWithItem(repository, Owner, priority: 4);
        var publisher = new FakeDomainEventPublisher();
        var interactor = new ChangeGiftItemPriorityInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(list.Id.Value, Owner.Value, itemId.Value, 4);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.PublishedBatches);
        Assert.DoesNotContain(nameof(FakeGiftListRepository.UpdateAsync), repository.Calls);
    }

    [Fact]
    public async Task Handle_ShouldSaveThenPublishOneEvent_WhenThePriorityChanges()
    {
        // Arrange
        var callLog = new CallLog();
        var repository = new FakeGiftListRepository { SharedLog = callLog };
        var (list, itemId) = SeededListWithItem(repository, Owner);
        var publisher = new FakeDomainEventPublisher { SharedLog = callLog };
        var interactor = new ChangeGiftItemPriorityInteractor(repository, publisher, new FakeClock(Now));
        var request = new ChangeGiftItemPriorityRequest(list.Id.Value, Owner.Value, itemId.Value, 5);

        // Act
        var result = await interactor.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, Assert.Single(list.Items).Priority.Value);
        Assert.Single(publisher.PublishedBatches);
        Assert.Equal(
            [
                nameof(FakeGiftListRepository.FindByIdAsync),
                nameof(FakeGiftListRepository.UpdateAsync),
                nameof(FakeDomainEventPublisher.PublishAsync),
            ],
            callLog.Entries);
    }
}
